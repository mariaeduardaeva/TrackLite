using SQLite;
using System.Collections.Generic;
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
        }

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

        public Task<int> SalvarCorridaAsync(Corrida corrida)
        {
            if (corrida.Id == 0)
                return _db.InsertAsync(corrida);
            else
                return _db.UpdateAsync(corrida);
        }

        public async Task MoverParaLixeiraAsync(Corrida corrida)
        {
            corrida.Lixeira = true;
            await SalvarCorridaAsync(corrida);
        }

        public async Task RestaurarCorridaAsync(Corrida corrida)
        {
            corrida.Lixeira = false;
            await SalvarCorridaAsync(corrida);
        }

        public Task<int> DeleteCorridaAsync(Corrida corrida)
        {
            return _db.DeleteAsync(corrida);
        }
    }
}