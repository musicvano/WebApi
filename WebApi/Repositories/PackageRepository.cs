using Npgsql;
using WebApi.Models;

namespace WebApi.Repositories
{
    public class PackageRepository(NpgsqlDataSource dataSource)
    {
        private const string Columns = "id, name, description, repository, license, created";

        private readonly NpgsqlDataSource dataSource = dataSource;

        public async Task<List<Package>> GetAllAsync()
        {
            await using var command = dataSource.CreateCommand(
                $"SELECT {Columns} FROM packages ORDER BY name");
            var packages = new List<Package>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                packages.Add(ReadPackage(reader));
            }
            return packages;
        }

        public async Task<Package?> GetByIdAsync(Guid id)
        {
            await using var command = dataSource.CreateCommand(
                $"SELECT {Columns} FROM packages WHERE id = $1");
            command.Parameters.AddWithValue(id);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }
            return ReadPackage(reader);
        }

        public async Task<Package?> GetByNameAsync(string name)
        {
            await using var command = dataSource.CreateCommand(
                $"SELECT {Columns} FROM packages WHERE name = $1");
            command.Parameters.AddWithValue(name);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }
            return ReadPackage(reader);
        }

        public async Task CreateAsync(Package package)
        {
            await using var command = dataSource.CreateCommand(
                "INSERT INTO packages (id, name, description, repository, license, created) " +
                "VALUES ($1, $2, $3, $4, $5, $6)");
            command.Parameters.AddWithValue(package.Id);
            command.Parameters.AddWithValue(package.Name);
            command.Parameters.AddWithValue(package.Description);
            command.Parameters.AddWithValue(package.Repository);
            command.Parameters.AddWithValue(package.License);
            command.Parameters.AddWithValue(package.Created);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<bool> UpdateAsync(Package package)
        {
            await using var command = dataSource.CreateCommand(
                "UPDATE packages SET name = $2, description = $3, repository = $4, license = $5 " +
                "WHERE id = $1");
            command.Parameters.AddWithValue(package.Id);
            command.Parameters.AddWithValue(package.Name);
            command.Parameters.AddWithValue(package.Description);
            command.Parameters.AddWithValue(package.Repository);
            command.Parameters.AddWithValue(package.License);
            return await command.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            await using var command = dataSource.CreateCommand(
                "DELETE FROM packages WHERE id = $1");
            command.Parameters.AddWithValue(id);
            return await command.ExecuteNonQueryAsync() > 0;
        }

        private static Package ReadPackage(NpgsqlDataReader reader) => new()
        {
            Id = reader.GetGuid(0),
            Name = reader.GetString(1),
            Description = reader.GetString(2),
            Repository = reader.GetString(3),
            License = reader.GetString(4),
            Created = reader.GetFieldValue<DateTime>(5)
        };
    }
}
