using Npgsql;
using WebApi.Models;

namespace WebApi.Repositories
{
    public class WorkflowRepository(NpgsqlDataSource dataSource)
    {
        private const string Columns =
            "name, build_conclusion, build_completed, test_conclusion, " +
            "test_completed, deploy_conclusion, deploy_completed";

        private static readonly Dictionary<string, string> Jobs =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["build"] = "build",
                ["test"] = "test",
                ["deploy"] = "deploy",
                ["ci / Build"] = "build",
                ["ci / Test"] = "test"
            };

        private readonly NpgsqlDataSource dataSource = dataSource;

        public async Task<List<Workflow>> GetAllAsync()
        {
            await using var command = dataSource.CreateCommand(
                $"SELECT {Columns} FROM workflows ORDER BY name");
            var workflows = new List<Workflow>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                workflows.Add(ReadWorkflow(reader));
            }
            return workflows;
        }

        public async Task<bool> UpsertJobAsync(
            string workflowName, string job, string? conclusion, DateTime? completed)
        {
            if (!Jobs.TryGetValue(job, out var column))
            {
                return false;
            }
            var conclusionColumn = $"{column}_conclusion";
            var completedColumn = $"{column}_completed";
            await using var command = dataSource.CreateCommand(
                $"INSERT INTO workflows (name, {conclusionColumn}, {completedColumn}) " +
                "VALUES ($1, $2, $3) " +
                $"ON CONFLICT (name) DO UPDATE SET {conclusionColumn} = $2, {completedColumn} = $3");
            command.Parameters.AddWithValue(workflowName);
            command.Parameters.AddWithValue((object?)conclusion ?? DBNull.Value);
            command.Parameters.AddWithValue((object?)completed ?? DBNull.Value);
            await command.ExecuteNonQueryAsync();
            return true;
        }

        private static Workflow ReadWorkflow(NpgsqlDataReader reader) => new()
        {
            Name = reader.GetString(0),
            BuildConclusion = reader.IsDBNull(1) ? null : reader.GetString(1),
            BuildCompleted = reader.IsDBNull(2) ? null : reader.GetFieldValue<DateTime>(2),
            TestConclusion = reader.IsDBNull(3) ? null : reader.GetString(3),
            TestCompleted = reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTime>(4),
            DeployConclusion = reader.IsDBNull(5) ? null : reader.GetString(5),
            DeployCompleted = reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTime>(6),
        };
    }
}
