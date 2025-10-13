using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TrackLite.Models;

namespace TrackLite.Services
{
    public class DatabaseService
    {
        private readonly SQLiteAsyncConnection _db;

        public DatabaseService()
        {
            string dbPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "tracklite.db3"
            );

            _db = new SQLiteAsyncConnection(dbPath);

            _db.CreateTableAsync<Corrida>().Wait();
            _db.CreateTableAsync<ActivityPoint>().Wait();
        }

        #region CRUD Corrida (Mantendo compatibilidade)
        public Task<List<Corrida>> GetHistoricoAsync()
        {
            return _db.Table<Corrida>()
                      .Where(c => !c.Lixeira)
                      .OrderByDescending(c => c.Data)
                      .ToListAsync();
        }

        public Task<List<Corrida>> GetLixeiraAsync()
        {
            return _db.Table<Corrida>()
                      .Where(c => c.Lixeira)
                      .OrderByDescending(c => c.Data)
                      .ToListAsync();
        }

        public async Task<int> SalvarCorridaAsync(Corrida corrida, bool dataAntiga = false)
        {
            if (dataAntiga)
            {
                corrida.Data = DateTime.Now.AddDays(-40);
                corrida.StartTime = DateTime.Now.AddDays(-40);
            }

            if (corrida.StartTime == default)
                corrida.StartTime = corrida.Data;

            corrida.UpdatedAt = DateTime.Now;

            if (corrida.Id == 0)
            {
                corrida.CreatedAt = DateTime.Now;
                return await _db.InsertAsync(corrida);
            }
            else
            {
                return await _db.UpdateAsync(corrida);
            }
        }

        public async Task MoverParaLixeiraAsync(Corrida corrida)
        {
            corrida.Lixeira = true;
            corrida.UpdatedAt = DateTime.Now;
            await SalvarCorridaAsync(corrida);
        }

        public async Task RestaurarCorridaAsync(Corrida corrida)
        {
            corrida.Lixeira = false;
            corrida.UpdatedAt = DateTime.Now;
            await SalvarCorridaAsync(corrida);
        }

        public Task<int> DeleteCorridaAsync(Corrida corrida)
        {
            return _db.DeleteAsync(corrida);
        }
        #endregion

        #region CRUD ActivityPoint (Novo - para RF18)
        public async Task<int> InserirPontosAsync(List<ActivityPoint> pontos)
        {
            return await _db.InsertAllAsync(pontos);
        }

        public Task<List<ActivityPoint>> GetPontosPorCorridaAsync(int corridaId)
        {
            return _db.Table<ActivityPoint>()
                      .Where(p => p.ActivityId == corridaId)
                      .OrderBy(p => p.Sequence)
                      .ToListAsync();
        }

        public async Task<int> DeletarPontosDaCorridaAsync(int corridaId)
        {
            var pontos = await _db.Table<ActivityPoint>()
                                 .Where(p => p.ActivityId == corridaId)
                                 .ToListAsync();

            foreach (var ponto in pontos)
            {
                await _db.DeleteAsync(ponto);
            }

            return pontos.Count;
        }
        #endregion

        #region Operação Atômica Simplificada
        public async Task SalvarCorridaComPontosAsync(Corrida corrida, List<ActivityPoint> pontos)
        {
            if (corrida.Id == 0)
            {
                corrida.CreatedAt = DateTime.Now;
                await _db.InsertAsync(corrida);
            }
            else
            {
                await _db.UpdateAsync(corrida);
            }

            foreach (var ponto in pontos)
            {
                ponto.ActivityId = corrida.Id;
            }
            await _db.InsertAllAsync(pontos);
        }
        #endregion

        public async Task RemoverLixeiraExpiradaAsync(int dias = 30)
        {
            var limite = DateTime.Now.AddDays(-dias);

            var corridasExpiradas = await _db.Table<Corrida>()
                                             .Where(c => c.Lixeira && c.Data < limite)
                                             .ToListAsync();

            foreach (var corrida in corridasExpiradas)
            {
                await DeletarPontosDaCorridaAsync(corrida.Id);

                await _db.DeleteAsync(corrida);
            }
        }
    }
}
