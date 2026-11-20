using System.Data;
using System.Data.Common;
using System.Net;
using System.Text.Json;
using ChecadorAPI.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Moq.Protected;
using Xunit;

namespace ChecadorAPI.Tests;

public sealed class WorkerScheduleTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ValidWorkerReturnsScheduleWithoutPin(bool needPin)
    {
        using var factory = new ScheduleFactory(CreateWorker(), needPin: needPin);
        using var client = CreateClient(factory);
        using var response = await client.GetAsync("/api/workers/42/schedule");
        var body = await AssertResponse(response, HttpStatusCode.OK, 1);
        Assert.Equal("Horarios obtenidos correctamente", body.GetProperty("mensaje").GetString());
        var data = body.GetProperty("data");
        Assert.Equal(6, data.EnumerateObject().Count());
        Assert.Equal(42, data.GetProperty("idTrabajador").GetInt32());
        Assert.Equal("Juan Garcia", data.GetProperty("nombreCompleto").GetString());
        Assert.Equal("08:00", data.GetProperty("horaEntrada").GetString());
        Assert.Equal("13:00", data.GetProperty("horaSalidaComida").GetString());
        Assert.Equal("14:00", data.GetProperty("horaEntradaComida").GetString());
        Assert.Equal("17:00", data.GetProperty("horaSalida").GetString());
        factory.ConnectionFactory.Verify(item => item.CreateConnection(), Times.Once);
        Assert.Contains("WHERE IDTrabajador = @idTrabajador", factory.Command.Object.CommandText);
        Assert.DoesNotContain("PIN", factory.Command.Object.CommandText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Contrasena", factory.Command.Object.CommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(42, factory.Parameter.Object.Value);
        Assert.Equal(DbType.Int32, factory.Parameter.Object.DbType);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrInactiveWorkerReturnsBusinessWarning(bool inactive)
    {
        var table = CreateWorker();
        if (inactive)
            table.Rows[0]["status"] = false;
        else
            table.Rows.Clear();
        using var factory = new ScheduleFactory(table);
        using var client = CreateClient(factory);
        using var response = await client.GetAsync("/api/workers/42/schedule");
        var body = await AssertResponse(response, HttpStatusCode.BadRequest, 2);
        Assert.Contains("no existe o esta inactivo", body.GetProperty("mensaje").GetString());
    }

    [Theory]
    [InlineData("HoraEntrada", null)]
    [InlineData("HoraSalidaComida", "")]
    [InlineData("HoraEntradaComida", "no-es-hora")]
    [InlineData("HoraSalida", "25:00")]
    public async Task MissingOrInvalidHourReturnsBusinessWarning(string column, string? value)
    {
        var table = CreateWorker();
        table.Rows[0][column] = (object?)value ?? DBNull.Value;
        using var factory = new ScheduleFactory(table);
        using var client = CreateClient(factory);
        using var response = await client.GetAsync("/api/workers/42/schedule");
        var body = await AssertResponse(response, HttpStatusCode.BadRequest, 2);
        Assert.Contains("horarios completos configurados", body.GetProperty("mensaje").GetString());
    }

    [Fact]
    public async Task SqlTimeValuesAreFormattedAsHoursAndMinutes()
    {
        var table = CreateWorker();
        table.Rows[0]["HoraEntrada"] = TimeSpan.FromHours(8);
        table.Rows[0]["HoraSalidaComida"] = TimeSpan.FromHours(13);
        table.Rows[0]["HoraEntradaComida"] = TimeSpan.FromHours(14);
        table.Rows[0]["HoraSalida"] = TimeSpan.FromHours(17);
        using var factory = new ScheduleFactory(table);
        using var client = CreateClient(factory);
        using var response = await client.GetAsync("/api/workers/42/schedule");
        var body = await AssertResponse(response, HttpStatusCode.OK, 1);
        Assert.Equal("08:00", body.GetProperty("data").GetProperty("horaEntrada").GetString());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("2147483648")]
    public async Task InvalidIdReturnsContractWithoutQueryingDatabase(string workerId)
    {
        using var factory = new ScheduleFactory(CreateWorker());
        using var client = CreateClient(factory);
        using var response = await client.GetAsync($"/api/workers/{workerId}/schedule");
        await AssertResponse(response, HttpStatusCode.BadRequest, 0);
        factory.ConnectionFactory.Verify(item => item.CreateConnection(), Times.Never);
    }

    [Fact]
    public async Task DatabaseFailureReturnsControlledErrorWithoutInternalDetails()
    {
        using var factory = new ScheduleFactory(CreateWorker(), fail: true);
        using var client = CreateClient(factory);
        using var response = await client.GetAsync("/api/workers/42/schedule");
        var body = await AssertResponse(response, HttpStatusCode.InternalServerError, 0);
        Assert.Equal("No fue posible obtener los horarios del trabajador.", body.GetProperty("mensaje").GetString());
        Assert.DoesNotContain("internal-database-detail", body.GetRawText());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("wrong-key")]
    public async Task MissingOrInvalidApiKeyBlocksDatabaseAccess(string? apiKey)
    {
        using var factory = new ScheduleFactory(CreateWorker());
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        if (apiKey is not null)
            client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        using var response = await client.GetAsync("/api/workers/42/schedule");
        await AssertResponse(response, HttpStatusCode.Unauthorized, 0);
        factory.ConnectionFactory.Verify(item => item.CreateConnection(), Times.Never);
    }

    [Fact]
    public async Task SwaggerIncludesRouteResponseCodesAndApiKeySecurity()
    {
        using var factory = new ScheduleFactory(CreateWorker());
        using var client = CreateClient(factory);
        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var operation = document.RootElement.GetProperty("paths").GetProperty("/api/workers/{idTrabajador}/schedule").GetProperty("get");
        foreach (var status in new[] { "200", "400", "401", "500" })
            Assert.True(operation.GetProperty("responses").TryGetProperty(status, out _));
        Assert.True(document.RootElement.GetProperty("security")[0].TryGetProperty("ApiKey", out _));
    }

    private static HttpClient CreateClient(ScheduleFactory factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Add("X-Api-Key", "schedule-test-key");
        return client;
    }

    private static async Task<JsonElement> AssertResponse(HttpResponseMessage response, HttpStatusCode status, int exito)
    {
        Assert.Equal(status, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = document.RootElement.Clone();
        Assert.Equal(new[] { "data", "exito", "mensaje" }, body.EnumerateObject().Select(property => property.Name).OrderBy(name => name));
        Assert.Equal(exito, body.GetProperty("exito").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("mensaje").GetString()));
        if (exito != 1)
            Assert.Equal(JsonValueKind.Null, body.GetProperty("data").ValueKind);
        return body;
    }

    private static DataTable CreateWorker()
    {
        var table = new DataTable();
        foreach (var column in new[] { "IDTrabajador", "Nombre", "ApellidoP", "ApellidoM", "status", "HoraEntrada", "HoraSalidaComida", "HoraEntradaComida", "HoraSalida" })
            table.Columns.Add(column, typeof(object));
        table.Rows.Add(42, "Juan", "Garcia", DBNull.Value, true, "08:00:00", "13:00", "14:00", "17:00");
        return table;
    }

    private sealed class ScheduleFactory(DataTable table, bool fail = false, bool needPin = true) : WebApplicationFactory<Program>
    {
        public Mock<ISqlConnectionFactory> ConnectionFactory { get; } = new();
        public Mock<DbCommand> Command { get; } = new();
        public Mock<DbParameter> Parameter { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ApiSettings:ApiKey"] = "schedule-test-key",
                ["ApiSettings:NeedPin"] = needPin.ToString()
            }));
            builder.ConfigureTestServices(services =>
            {
                var connection = new Mock<DbConnection>();
                Command.SetupAllProperties();
                Parameter.SetupAllProperties();
                Command.Protected().Setup<DbParameter>("CreateDbParameter").Returns(Parameter.Object);
                Command.Protected().Setup<DbParameterCollection>("DbParameterCollection").Returns(Mock.Of<DbParameterCollection>());
                Command.Protected().Setup<Task<DbDataReader>>("ExecuteDbDataReaderAsync", ItExpr.IsAny<CommandBehavior>(), ItExpr.IsAny<CancellationToken>())
                    .Returns(() => Task.FromResult<DbDataReader>(table.CreateDataReader()));
                connection.Protected().Setup<DbCommand>("CreateDbCommand").Returns(Command.Object);
                if (fail)
                    connection.Setup(item => item.OpenAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("internal-database-detail"));
                else
                    connection.Setup(item => item.OpenAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
                ConnectionFactory.Setup(item => item.CreateConnection()).Returns(connection.Object);
                services.RemoveAll<ISqlConnectionFactory>();
                services.AddSingleton(ConnectionFactory.Object);
            });
        }
    }
}