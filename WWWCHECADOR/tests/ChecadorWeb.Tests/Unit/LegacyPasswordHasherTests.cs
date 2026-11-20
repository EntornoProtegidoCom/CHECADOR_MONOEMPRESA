using ChecadorWeb.Security;

namespace ChecadorWeb.Tests.Unit;

public class LegacyPasswordHasherTests
{
    // Valor obtenido en SQL Server: CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', N'Prueba#2026'), 2)
    private const string HashSql = "42BF420526B8F28999E36B684637B7C3E9A2FB713EF5A8E2F81900895E130EAD";

    [Fact]
    public void Hash_coincide_con_HASHBYTES_de_SqlServer()
    {
        Assert.Equal(HashSql, LegacyPasswordHasher.Hash("Prueba#2026"));
    }

    [Theory]
    [InlineData(HashSql)]
    [InlineData("42bf420526b8f28999e36b684637b7c3e9a2fb713ef5a8e2f81900895e130ead")]
    [InlineData(" 42BF420526B8F28999E36B684637B7C3E9A2FB713EF5A8E2F81900895E130EAD ")]
    public void Verify_acepta_hash_almacenado_sin_importar_mayusculas_ni_espacios(string almacenado)
    {
        Assert.True(LegacyPasswordHasher.Verify("Prueba#2026", almacenado));
    }

    [Theory]
    [InlineData("prueba#2026")]
    [InlineData("Prueba#2026 ")]
    [InlineData("")]
    public void Verify_rechaza_contrasenas_distintas(string password)
    {
        Assert.False(LegacyPasswordHasher.Verify(password, HashSql));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Verify_rechaza_hash_vacio(string? almacenado)
    {
        Assert.False(LegacyPasswordHasher.Verify("Prueba#2026", almacenado));
    }

    [Fact]
    public void Sello_cambia_al_cambiar_la_contrasena_y_no_expone_el_hash()
    {
        var a = LegacyPasswordHasher.Sello(HashSql);
        var b = LegacyPasswordHasher.Sello(LegacyPasswordHasher.Hash("Otra#2026"));
        Assert.NotEqual(a, b);
        Assert.Equal(a, LegacyPasswordHasher.Sello(HashSql.ToLowerInvariant()));
        Assert.DoesNotContain(a, HashSql);
        Assert.Equal(16, a.Length);
    }
}
