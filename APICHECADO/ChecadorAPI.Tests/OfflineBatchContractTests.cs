using System.Xml.Linq;
using ChecadorAPI.Controllers;
using ChecadorAPI.Data;
using ChecadorAPI.Models.DTOs;
using ChecadorAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace ChecadorAPI.Tests;

public sealed class OfflineBatchContractTests
{
    [Theory]
    [InlineData(3, 0)]
    [InlineData(2, 1)]
    [InlineData(0, 3)]
    public async Task ControllerReturnsProcessedEnvelopeRegardlessOfIndividualFailures(int successful, int failed)
    {
        var requests = Enumerable.Range(1, successful + failed).Select(index => new MarcajeRequestDto
        {
            GuidRegistro = $"synthetic-{index}",
            FechaHoraCaptura = new DateTime(2026, 1, 1, 8, 0, 0).AddMinutes(index * 10)
        }).ToList();
        var batch = new MarcajeBatchRequestDto { Registros = requests };
        var data = new MarcajeBatchResultDto
        {
            TotalProcesados = requests.Count,
            Exitosos = successful,
            Fallidos = failed,
            Mensajes = requests.Select((request, index) => index < successful
                ? $"GUID {request.GuidRegistro}: Exito simulado"
                : $"GUID {request.GuidRegistro} [Error]: Rechazo simulado").ToList()
        };
        var service = new Mock<IAsistenciaService>();
        service.Setup(item => item.ProcesarLoteOfflineAsync(batch)).ReturnsAsync(
            ApiResponseDto<MarcajeBatchResultDto>.Ok(data));
        var controller = new AsistenciaController(service.Object, Mock.Of<ISqlConnectionFactory>());

        var response = await controller.MarcajeOffline(batch);

        var http = Assert.IsType<OkObjectResult>(response.Result);
        var envelope = Assert.IsType<ApiResponseDto<MarcajeBatchResultDto>>(http.Value);
        Assert.Equal(1, envelope.Exito);
        Assert.NotNull(envelope.Data);
        Assert.Equal(successful, envelope.Data.Exitosos);
        Assert.Equal(failed, envelope.Data.Fallidos);
        Assert.Equal(envelope.Data.TotalProcesados, envelope.Data.Exitosos + envelope.Data.Fallidos);
        Assert.Equal(requests.Count, envelope.Data.Mensajes.Count);
        foreach (var request in requests)
            Assert.Single(envelope.Data.Mensajes, message => message.Contains(request.GuidRegistro));
    }

    [Fact]
    public async Task ActualBatchServiceReturnsExitoOneEvenWhenAllSyntheticRequestsFailValidation()
    {
        var factory = new Mock<ISqlConnectionFactory>(MockBehavior.Strict);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApiSettings:NeedPin"] = "false"
        }).Build();
        var service = new AsistenciaService(factory.Object, configuration, NullLogger<AsistenciaService>.Instance);
        var batch = new MarcajeBatchRequestDto
        {
            Registros = Enumerable.Range(1, 3).Select(index => new MarcajeRequestDto
            {
                GuidRegistro = $"invalid-synthetic-{index}",
                IdTrabajador = 0,
                Pin = 0,
                TipoRegistro = 1,
                FechaHoraCaptura = new DateTime(2026, 1, 1).AddMinutes(index),
                NbEquipo = "SYNTHETIC"
            }).ToList()
        };

        var response = await service.ProcesarLoteOfflineAsync(batch);

        Assert.Equal(1, response.Exito);
        Assert.NotNull(response.Data);
        Assert.Equal(0, response.Data.Exitosos);
        Assert.Equal(3, response.Data.Fallidos);
        Assert.All(batch.Registros, request => Assert.Contains(response.Data.Mensajes,
            message => message.Contains(request.GuidRegistro) && message.Contains("[Error]")));
        factory.Verify(item => item.CreateConnection(), Times.Never);
    }

    [Fact]
    public void OfflineXmlOmitsNullPinAndDoesNotInventHashOrGuid()
    {
        int? pin = 0;
        var pinForPayload = pin.HasValue && pin.Value > 0 ? pin : null;
        var xml = XmlPayloadBuilder.BuildXml("O", new()
        {
            ["IDTrabajador"] = 1,
            ["PIN"] = pinForPayload,
            ["TipoRegistro"] = 1,
            ["Horaregistro"] = "2026-11-02T14:28:31",
            ["IPRegistro"] = "127.0.0.1",
            ["NBEquipo"] = "LPENTORNO165"
        });

        var root = XElement.Parse(xml);
        Assert.Equal("O", root.Element("accion")?.Value);
        Assert.Null(root.Element("PIN"));
        Assert.Null(root.Element("HashBuscar"));
        Assert.Null(root.Element("GuidRegistro"));
        Assert.Equal("2026-11-02T14:28:31", root.Element("Horaregistro")?.Value);
    }
}