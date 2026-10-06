using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PijacaUDzepu.API.Models;
using PijacaUDzepu.API.Models.Enums;

namespace PijacaUDzepu.API.DataAccess.SeedData;

public static class Seed
{
    public static async Task SeedData(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SeedData");
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();

        logger.LogInformation("Starting database migration...");
        await context.Database.MigrateAsync();
        logger.LogInformation("Migration complete.");

        var roles = new[] { "SuperAdmin", "VendorAdmin", "Customer" };
        foreach (var roleName in roles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
                await roleManager.CreateAsync(new Role { Name = roleName });
        }

        if (!await userManager.Users.AnyAsync(u => u.UserName == "admin"))
        {
            logger.LogInformation("Creating admin user...");
            var admin = new User
            {
                UserName = "admin",
                FirstName = "Super",
                LastName = "Admin",
                Email = "admin@pijacaudzepu.rs"
            };
            var result = await userManager.CreateAsync(admin, "admin1234");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "SuperAdmin");
                logger.LogInformation("Admin user created successfully.");
            }
            else
            {
                logger.LogError("Failed to create admin user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
        else
        {
            logger.LogInformation("Admin user already exists.");
        }

        if (!await userManager.Users.AnyAsync(u => u.UserName == "kupac"))
        {
            var customer = new User
            {
                UserName = "kupac",
                FirstName = "Petar",
                LastName = "Petrović",
                Email = "petar@example.com"
            };
            var result = await userManager.CreateAsync(customer, "kupac1234");
            if (result.Succeeded)
                await userManager.AddToRoleAsync(customer, "Customer");
        }

        if (!await context.Markets.AnyAsync())
        {
            var markets = new List<Market>
            {
                new() { Name = "Futoška pijaca", Description = "Najveća novosadska pijaca sa širokim izborom svežeg voća, povrća i domaćih proizvoda.", Address = "Bulevar oslobođenja 30, Novi Sad", ImageUrl = "https://images.unsplash.com/photo-1526399743290-f73cb4022f48?w=600&h=400&fit=crop" },
                new() { Name = "Riblja pijaca", Description = "Tradicionalna pijaca u centru grada, poznata po ribljim specijalitetima i svežem povrću.", Address = "Jevrejska ulica, Novi Sad", ImageUrl = "https://images.unsplash.com/photo-1514425263458-109317cc1321?w=600&h=400&fit=crop" },
                new() { Name = "Limanska pijaca", Description = "Moderna pijaca na Limanu sa odličnom ponudom voća, povrća i začina.", Address = "Narodnog fronta 2, Novi Sad", ImageUrl = "https://images.unsplash.com/photo-1567306295427-94503f8300d7?w=600&h=400&fit=crop" },
                new() { Name = "Najlon pijaca", Description = "Poznata novosadska pijaca sa raznovrsnom ponudom po pristupačnim cenama.", Address = "Temerinski put, Novi Sad", ImageUrl = "https://images.unsplash.com/photo-1686529896385-8a8d581d0225?w=600&h=400&fit=crop" },
            };

            context.Markets.AddRange(markets);
            await context.SaveChangesAsync();

            var stalls = new List<Stall>
            {
                // Futoška pijaca (markets[0]) - 20 tezgi
                new() { MarketId = markets[0].Id, Label = "1" },
                new() { MarketId = markets[0].Id, Label = "2" },
                new() { MarketId = markets[0].Id, Label = "3" },
                new() { MarketId = markets[0].Id, Label = "4" },
                new() { MarketId = markets[0].Id, Label = "5" },
                new() { MarketId = markets[0].Id, Label = "6" },
                new() { MarketId = markets[0].Id, Label = "7" },
                new() { MarketId = markets[0].Id, Label = "8" },
                new() { MarketId = markets[0].Id, Label = "9" },
                new() { MarketId = markets[0].Id, Label = "10" },
                new() { MarketId = markets[0].Id, Label = "11" },
                new() { MarketId = markets[0].Id, Label = "12" },
                new() { MarketId = markets[0].Id, Label = "13" },
                new() { MarketId = markets[0].Id, Label = "14" },
                new() { MarketId = markets[0].Id, Label = "15" },
                new() { MarketId = markets[0].Id, Label = "16" },
                new() { MarketId = markets[0].Id, Label = "17" },
                new() { MarketId = markets[0].Id, Label = "18" },
                new() { MarketId = markets[0].Id, Label = "19" },
                new() { MarketId = markets[0].Id, Label = "20" },

                // Riblja pijaca (markets[1]) - 15 tezgi
                new() { MarketId = markets[1].Id, Label = "1" },
                new() { MarketId = markets[1].Id, Label = "2" },
                new() { MarketId = markets[1].Id, Label = "3" },
                new() { MarketId = markets[1].Id, Label = "4" },
                new() { MarketId = markets[1].Id, Label = "5" },
                new() { MarketId = markets[1].Id, Label = "6" },
                new() { MarketId = markets[1].Id, Label = "7" },
                new() { MarketId = markets[1].Id, Label = "8" },
                new() { MarketId = markets[1].Id, Label = "9" },
                new() { MarketId = markets[1].Id, Label = "10" },
                new() { MarketId = markets[1].Id, Label = "11" },
                new() { MarketId = markets[1].Id, Label = "12" },
                new() { MarketId = markets[1].Id, Label = "13" },
                new() { MarketId = markets[1].Id, Label = "14" },
                new() { MarketId = markets[1].Id, Label = "15" },

                // Limanska pijaca (markets[2]) - 12 tezgi
                new() { MarketId = markets[2].Id, Label = "1" },
                new() { MarketId = markets[2].Id, Label = "2" },
                new() { MarketId = markets[2].Id, Label = "3" },
                new() { MarketId = markets[2].Id, Label = "4" },
                new() { MarketId = markets[2].Id, Label = "5" },
                new() { MarketId = markets[2].Id, Label = "6" },
                new() { MarketId = markets[2].Id, Label = "7" },
                new() { MarketId = markets[2].Id, Label = "8" },
                new() { MarketId = markets[2].Id, Label = "9" },
                new() { MarketId = markets[2].Id, Label = "10" },
                new() { MarketId = markets[2].Id, Label = "11" },
                new() { MarketId = markets[2].Id, Label = "12" },

                // Najlon pijaca (markets[3]) - 25 tezgi
                new() { MarketId = markets[3].Id, Label = "1" },
                new() { MarketId = markets[3].Id, Label = "2" },
                new() { MarketId = markets[3].Id, Label = "3" },
                new() { MarketId = markets[3].Id, Label = "4" },
                new() { MarketId = markets[3].Id, Label = "5" },
                new() { MarketId = markets[3].Id, Label = "6" },
                new() { MarketId = markets[3].Id, Label = "7" },
                new() { MarketId = markets[3].Id, Label = "8" },
                new() { MarketId = markets[3].Id, Label = "9" },
                new() { MarketId = markets[3].Id, Label = "10" },
                new() { MarketId = markets[3].Id, Label = "11" },
                new() { MarketId = markets[3].Id, Label = "12" },
                new() { MarketId = markets[3].Id, Label = "13" },
                new() { MarketId = markets[3].Id, Label = "14" },
                new() { MarketId = markets[3].Id, Label = "15" },
                new() { MarketId = markets[3].Id, Label = "16" },
                new() { MarketId = markets[3].Id, Label = "17" },
                new() { MarketId = markets[3].Id, Label = "18" },
                new() { MarketId = markets[3].Id, Label = "19" },
                new() { MarketId = markets[3].Id, Label = "20" },
                new() { MarketId = markets[3].Id, Label = "21" },
                new() { MarketId = markets[3].Id, Label = "22" },
                new() { MarketId = markets[3].Id, Label = "23" },
                new() { MarketId = markets[3].Id, Label = "24" },
                new() { MarketId = markets[3].Id, Label = "25" },
            };

            context.Stalls.AddRange(stalls);
            await context.SaveChangesAsync();

            // Helper to find stall by market index and label
            Stall findStall(int marketIdx, string label) =>
                stalls.First(s => s.MarketId == markets[marketIdx].Id && s.Label == label);

            var vendors = new List<Vendor>
            {
                // Futoška pijaca
                new() { MarketId = markets[0].Id, StallId = findStall(0, "12").Id, Name = "Marko Voće", Description = "Svežo voće i povrće pravo sa njive iz Šumadije.", Phone = "064 123 4567" },
                new() { MarketId = markets[0].Id, StallId = findStall(0, "5").Id, Name = "Jovana Organic", Description = "Sertifikovana organska proizvodnja bez pesticida.", Phone = "065 987 6543" },
                new() { MarketId = markets[0].Id, StallId = findStall(0, "18").Id, Name = "Zeleni Raj", Description = "Začinsko bilje, zeleniš i sezonsko povrće.", Phone = "063 222 3456" },

                // Riblja pijaca
                new() { MarketId = markets[1].Id, StallId = findStall(1, "7").Id, Name = "Salaš Subotički", Description = "Vojvođanski salaš sa tradicijom dugom 50 godina.", Phone = "069 555 1234" },
                new() { MarketId = markets[1].Id, StallId = findStall(1, "3").Id, Name = "Bašta Niš", Description = "Južnjačko voće i povrće po najpovoljnijim cenama.", Phone = "062 444 7890" },

                // Limanska pijaca
                new() { MarketId = markets[2].Id, StallId = findStall(2, "9").Id, Name = "Fruška Gora Bio", Description = "Domaći proizvodi sa Fruške Gore - med, voće, povrće.", Phone = "066 333 2211" },
                new() { MarketId = markets[2].Id, StallId = findStall(2, "2").Id, Name = "Đurđev Salaš", Description = "Svežo povrće od plastenika do vaše trpeze.", Phone = "064 777 8899" },

                // Najlon pijaca
                new() { MarketId = markets[3].Id, StallId = findStall(3, "14").Id, Name = "Braća Marković", Description = "Porodična tradicija prodaje voća i povrća od 1985.", Phone = "065 111 4455" },
                new() { MarketId = markets[3].Id, StallId = findStall(3, "22").Id, Name = "Sremski Plodovi", Description = "Kvalitetno povrće iz srca Srema.", Phone = "069 888 3322" },
            };

            context.Vendors.AddRange(vendors);
            await context.SaveChangesAsync();

            var vendorUsers = new[]
            {
                new { UserName = "marko", FirstName = "Marko", LastName = "Jovanović", VendorIndex = 0 },
                new { UserName = "jovana", FirstName = "Jovana", LastName = "Nikolić", VendorIndex = 1 },
                new { UserName = "nikola", FirstName = "Nikola", LastName = "Đorđević", VendorIndex = 2 },
                new { UserName = "milan", FirstName = "Milan", LastName = "Horvat", VendorIndex = 3 },
                new { UserName = "jelena", FirstName = "Jelena", LastName = "Stanković", VendorIndex = 4 },
                new { UserName = "dragan", FirstName = "Dragan", LastName = "Milić", VendorIndex = 5 },
                new { UserName = "ana", FirstName = "Ana", LastName = "Savić", VendorIndex = 6 },
                new { UserName = "bojan", FirstName = "Bojan", LastName = "Marković", VendorIndex = 7 },
                new { UserName = "ivana", FirstName = "Ivana", LastName = "Kovačević", VendorIndex = 8 },
            };

            foreach (var vu in vendorUsers)
            {
                if (!await userManager.Users.AnyAsync(u => u.UserName == vu.UserName))
                {
                    var vendorUser = new User { UserName = vu.UserName, FirstName = vu.FirstName, LastName = vu.LastName };
                    var result = await userManager.CreateAsync(vendorUser, "vendor1234");
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(vendorUser, "VendorAdmin");
                        context.VendorUsers.Add(new VendorUser { UserId = vendorUser.Id, VendorId = vendors[vu.VendorIndex].Id });
                    }
                }
            }
            await context.SaveChangesAsync();

            var products = new List<Product>
            {
                // Marko Voće (Futoška)
                new() { VendorId = vendors[0].Id, Name = "Jabuke Crveni Delišes", Price = 150, Unit = ProductUnit.Kg, Category = ProductCategory.Voce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1560806887-1e4cd0b6cbd6?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[0].Id, Name = "Kruške Viljamovka", Price = 200, Unit = ProductUnit.Kg, Category = ProductCategory.Voce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1615484477778-ca3b77940c25?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[0].Id, Name = "Šljive Čačanka", Price = 120, Unit = ProductUnit.Kg, Category = ProductCategory.Voce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1568477070800-66719cd52be2?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[0].Id, Name = "Paradajz Domaći", Price = 200, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1582284540020-8acbe03f4924?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[0].Id, Name = "Paprike Babura", Price = 180, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1563565375-f3fdfdbefa83?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[0].Id, Name = "Krompir Mladi", Price = 100, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1518977676601-b53f82aba655?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[0].Id, Name = "Lubenica", Price = 50, Unit = ProductUnit.Kg, Category = ProductCategory.Voce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1589984662742-a13a59b2a237?w=400&h=300&fit=crop" },

                // Jovana Organic (Futoška)
                new() { VendorId = vendors[1].Id, Name = "Organski Paradajz", Price = 350, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1607305387299-a3d9611cd469?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[1].Id, Name = "Organske Tikvice", Price = 280, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1563252722-6434563a985d?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[1].Id, Name = "Organski Krastavci", Price = 250, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1604977042946-1eecc30259b0?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[1].Id, Name = "Organska Rukola", Price = 150, Unit = ProductUnit.Veza, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1576045057995-568f588f82fb?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[1].Id, Name = "Bio Cvekla", Price = 180, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1593105544559-ecb03bf76f82?w=400&h=300&fit=crop" },

                // Zeleni Raj (Futoška)
                new() { VendorId = vendors[2].Id, Name = "Bosiljak Sveži", Price = 80, Unit = ProductUnit.Veza, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1618164435735-413d3b066c9a?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[2].Id, Name = "Peršun", Price = 60, Unit = ProductUnit.Veza, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1599629954294-16196fbd23e0?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[2].Id, Name = "Mirođija", Price = 60, Unit = ProductUnit.Veza, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1615485290382-441e4d049cb5?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[2].Id, Name = "Zeleni Luk", Price = 50, Unit = ProductUnit.Veza, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1587049352846-4a222e784d38?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[2].Id, Name = "Nana Sveža", Price = 70, Unit = ProductUnit.Veza, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1628556270448-4d4e4148e1b1?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[2].Id, Name = "Rotkvice", Price = 100, Unit = ProductUnit.Veza, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1585502781079-d2e4c2a83e5e?w=400&h=300&fit=crop" },

                // Salaš Subotički (Riblja)
                new() { VendorId = vendors[3].Id, Name = "Crvena Paprika Rog", Price = 220, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1601648764658-cf37e8c89b70?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[3].Id, Name = "Kupus Zimski", Price = 60, Unit = ProductUnit.Komad, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1594282486552-05b4d80fbb9f?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[3].Id, Name = "Boranija", Price = 300, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1567375698348-5d9d5ae10c3a?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[3].Id, Name = "Dinja Medena", Price = 120, Unit = ProductUnit.Kg, Category = ProductCategory.Voce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1598170845058-32b9d6a5da37?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[3].Id, Name = "Kukuruz Šećerac", Price = 40, Unit = ProductUnit.Komad, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1551754655-cd27e38d2076?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[3].Id, Name = "Breskve", Price = 250, Unit = ProductUnit.Kg, Category = ProductCategory.Voce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1595124216650-1f02fe30bfaa?w=400&h=300&fit=crop" },

                // Bašta Niš (Riblja)
                new() { VendorId = vendors[4].Id, Name = "Ljuta Papričica", Price = 400, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1526470303-82c787d88682?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[4].Id, Name = "Patlidžan", Price = 200, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1615485290382-441e4d049cb5?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[4].Id, Name = "Praziluk", Price = 160, Unit = ProductUnit.Veza, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1567539549213-cc1697632146?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[4].Id, Name = "Zelena Salata", Price = 80, Unit = ProductUnit.Komad, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1622206151226-18ca2c9ab4a1?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[4].Id, Name = "Spanać", Price = 200, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1576045057995-568f588f82fb?w=400&h=300&fit=crop" },

                // Fruška Gora Bio (Limanska)
                new() { VendorId = vendors[5].Id, Name = "Domaći Med Lipov", Price = 1200, Unit = ProductUnit.Kg, Category = ProductCategory.Ostalo, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1587049352846-4a222e784d38?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[5].Id, Name = "Trešnje", Price = 350, Unit = ProductUnit.Kg, Category = ProductCategory.Voce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1528821154947-1aa3d1b74941?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[5].Id, Name = "Višnje", Price = 300, Unit = ProductUnit.Kg, Category = ProductCategory.Voce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1551829142-d9b8cf2c9232?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[5].Id, Name = "Maline", Price = 500, Unit = ProductUnit.Kg, Category = ProductCategory.Voce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1577069861033-55d04cec4ef5?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[5].Id, Name = "Kupine", Price = 450, Unit = ProductUnit.Kg, Category = ProductCategory.Voce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1615485290382-441e4d049cb5?w=400&h=300&fit=crop" },

                // Đurđev Salaš (Limanska)
                new() { VendorId = vendors[6].Id, Name = "Paradajz Cherry", Price = 300, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1661811820259-2575b82101bf?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[6].Id, Name = "Paprike Šilje", Price = 200, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1526470303-82c787d88682?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[6].Id, Name = "Krastavci Kornišoni", Price = 180, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1604977042946-1eecc30259b0?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[6].Id, Name = "Tikvice", Price = 150, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1563252722-6434563a985d?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[6].Id, Name = "Šargarepa", Price = 130, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1598170845058-32b9d6a5da37?w=400&h=300&fit=crop" },

                // Braća Marković (Najlon)
                new() { VendorId = vendors[7].Id, Name = "Krompir Crveni", Price = 80, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1518977676601-b53f82aba655?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[7].Id, Name = "Luk Crni", Price = 130, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1587049352846-4a222e784d38?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[7].Id, Name = "Beli Luk", Price = 800, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1540148426945-6cf22a6b2571?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[7].Id, Name = "Grašak Svež", Price = 350, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1563636619-e9143da7973b?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[7].Id, Name = "Pasulj Tetovac", Price = 600, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1551462147-ff29053bfc14?w=400&h=300&fit=crop" },

                // Sremski Plodovi (Najlon)
                new() { VendorId = vendors[8].Id, Name = "Paradajz Volovsko Srce", Price = 280, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1524593166156-312f362cada0?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[8].Id, Name = "Paprike Kalifornija", Price = 250, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1563565375-f3fdfdbefa83?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[8].Id, Name = "Karfiol", Price = 180, Unit = ProductUnit.Komad, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1568702846914-96b305d2aaeb?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[8].Id, Name = "Brokoli", Price = 250, Unit = ProductUnit.Komad, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1459411552884-841db9b3cc2a?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[8].Id, Name = "Kelj", Price = 100, Unit = ProductUnit.Komad, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1594282486552-05b4d80fbb9f?w=400&h=300&fit=crop" },
            };

            context.Products.AddRange(products);
            await context.SaveChangesAsync();
        }
    }
}
