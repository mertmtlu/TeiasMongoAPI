using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TeiasMongoAPI.Core.Models.KeyModels;
using TeiasMongoAPI.Data.Configuration;

namespace TeiasMongoAPI.Data.Context
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;

        public MongoDbContext(IOptions<MongoDbSettings> settings)
        {
            var client = new MongoClient(settings.Value.ConnectionString);
            _database = client.GetDatabase(settings.Value.DatabaseName);
        }

        // Collections
        public IMongoCollection<Client> Clients => _database.GetCollection<Client>("clients");
        public IMongoCollection<Region> Regions => _database.GetCollection<Region>("regions");
        public IMongoCollection<TM> TMs => _database.GetCollection<TM>("tms");
        public IMongoCollection<Building> Buildings => _database.GetCollection<Building>("buildings");
        public IMongoCollection<AlternativeTM> AlternativeTMs => _database.GetCollection<AlternativeTM>("alternativeTms");
    }
}
