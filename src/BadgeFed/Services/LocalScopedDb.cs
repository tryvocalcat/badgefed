using Microsoft.Extensions.Logging;
using System;
using System.Data.SQLite;
using System.Text.Json;
using BadgeFed.Models;

namespace BadgeFed.Services;

public class LocalScopedDb : LocalDbService
{
    public LocalScopedDb(string dbPath) : base(dbPath + ".db")
    {
    }

    public LocalScopedDb(IHttpContextAccessor httpContextAccessor)
        : base(
            !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SQLITE_DB_PATH"))
                ? Environment.GetEnvironmentVariable("SQLITE_DB_PATH") + ".db"
                : httpContextAccessor.HttpContext?.Request?.Host.Value + ".db"
          )
    {
        Console.WriteLine($"Using LocalScopedDb with path: {this.DbPath} for httpContext: {httpContextAccessor.HttpContext?.Request?.Host.Value}");
    }
}
