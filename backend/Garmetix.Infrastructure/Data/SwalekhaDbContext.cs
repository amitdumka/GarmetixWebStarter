using Microsoft.EntityFrameworkCore;

namespace Garmetix.Infrastructure.Data;

/// <summary>
/// A deliberately separate database/DbContext from GarmetixDbContext, backing the isolated
/// Swalekha (Personal &amp; Personal Finance) module. Data here belongs to a single Owner user,
/// not the multi-tenant Company/StoreGroup/Store hierarchy the rest of the platform uses -
/// entities added in later Swalekha stages should extend plain BaseEntity, not
/// CompanyBase/GroupBase/StoreBase. Same shared ASP.NET Core API process as GarmetixDbContext,
/// just a different Postgres database (see ConnectionStrings:Swalekha).
/// </summary>
public sealed class SwalekhaDbContext(DbContextOptions<SwalekhaDbContext> options) : DbContext(options)
{
}
