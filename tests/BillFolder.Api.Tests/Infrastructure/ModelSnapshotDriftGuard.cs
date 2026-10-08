using System.Security.Cryptography;
using System.Text;
using BillFolder.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace BillFolder.Api.Tests.Infrastructure;

public static class ModelSnapshotDriftGuard
{
    // Reviewed drift: manual SQL migrations for password reset, provisioned
    // expenses and refresh-token families, plus documented Npgsql enum churn.
    private const string AcceptedBaselineFingerprint =
        "22:0F92CEA40AA6572E31A0172FD02D7A1C240422AFBE302922A762E6A822750B12";

    public static void EnsureOnlyAcceptedBaseline(ApplicationDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);

        var actual = CalculateFingerprint(db);
        if (!string.Equals(actual, AcceptedBaselineFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"EF model drift changed. Expected {AcceptedBaselineFingerprint}, actual {actual}.");
        }
    }

    public static string CalculateFingerprint(ApplicationDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);

        var migrationsAssembly = db.GetService<IMigrationsAssembly>();
        var snapshot = migrationsAssembly.ModelSnapshot
            ?? throw new InvalidOperationException("EF migration snapshot is missing.");
        var modelInitializer = db.GetService<IModelRuntimeInitializer>();
        var snapshotModel = modelInitializer.Initialize(
            snapshot.Model,
            designTime: true,
            validationLogger: null);
        var currentModel = db.GetService<IDesignTimeModel>().Model;
        var modelDiffer = db.GetService<IMigrationsModelDiffer>();
        var operations = modelDiffer.GetDifferences(
            snapshotModel.GetRelationalModel(),
            currentModel.GetRelationalModel());
        var sqlGenerator = db.GetService<IMigrationsSqlGenerator>();
        var sql = string.Join(
            "\n-- operation --\n",
            sqlGenerator.Generate(operations, currentModel)
                .Select(command => command.CommandText.ReplaceLineEndings("\n").Trim()));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql)));

        return $"{operations.Count}:{hash}";
    }
}
