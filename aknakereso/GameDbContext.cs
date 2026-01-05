using Microsoft.EntityFrameworkCore;

public class GameDbContext : DbContext
{
    public DbSet<Result> Results { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlite("Data Source=aknakereso.db");
    }
}
