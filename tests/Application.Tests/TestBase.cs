using System;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Application.Tests;

public abstract class TestBase : IDisposable
{
    protected readonly ApplicationDbContext DbContext;
    protected readonly IUnitOfWork UnitOfWork;

    protected TestBase()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        
        DbContext = new ApplicationDbContext(options);
        UnitOfWork = new UnitOfWork<ApplicationDbContext>(DbContext);
    }

    public void Dispose()
    {
        DbContext?.Dispose();
    }
}

