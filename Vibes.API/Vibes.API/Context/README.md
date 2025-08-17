# To add ne migration
```shell
    dotnet ef migrations add InitialMigration `
    --startup-project Vibes.API `
    --context VibesContext `
    --output-dir Migrations --verbose
```