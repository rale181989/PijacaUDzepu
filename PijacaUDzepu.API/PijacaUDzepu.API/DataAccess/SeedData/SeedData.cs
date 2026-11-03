using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PijacaUDzepu.API.Models;
using PijacaUDzepu.API.Models.Enums;

namespace PijacaUDzepu.API.DataAccess.SeedData;

// Test nalozi (kupac, kuriri, prodavci) i pijace sa proizvodima prave se samo lokalno (Development)
// ili kada je DEMO_ACCOUNTS=true. U produkciji se admin pravi iz ADMIN_PASSWORD, a test nalozi koji
// još imaju poznatu podrazumevanu lozinku se zaključavaju.
public static class Seed
{
    private const string DefaultAdminPassword = "admin1234";

    private static readonly (string UserName, string Password)[] DemoAccounts =
    {
        ("kupac", "kupac1234"), ("kurir1", "kurir1234"), ("kurir2", "kurir1234"),
        ("marko", "vendor1234"), ("jovana", "vendor1234"), ("nikola", "vendor1234"), ("milan", "vendor1234"),
        ("jelena", "vendor1234"), ("dragan", "vendor1234"), ("ana", "vendor1234"), ("bojan", "vendor1234"),
        ("ivana", "vendor1234")
    };

    public static async Task SeedData(IServiceProvider serviceProvider, bool isDevelopment)
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SeedData");
        var context = scope.ServiceProvider.GetRequiredService<DataContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<Role>>();

        logger.LogInformation("Starting database migration...");
        await context.Database.MigrateAsync();
        logger.LogInformation("Migration complete.");

        var roles = new[] { "SuperAdmin", "VendorAdmin", "Customer", "Courier" };
        foreach (var roleName in roles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
                await roleManager.CreateAsync(new Role { Name = roleName });
        }

        await EnsureAdmin(userManager, isDevelopment, logger);
        await FixDemoData(context);

        var demo = isDevelopment || Environment.GetEnvironmentVariable("DEMO_ACCOUNTS") == "true";
        if (!demo)
        {
            await LockDemoAccounts(userManager, logger);
            return;
        }
        await UnlockDemoAccounts(userManager);

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

        var couriers = new[]
        {
            new { UserName = "kurir1", FirstName = "Stefan", LastName = "Popović", Phone = "064 100 2001" },
            new { UserName = "kurir2", FirstName = "Milica", LastName = "Ilić", Phone = "064 100 2002" },
        };
        foreach (var c in couriers)
        {
            if (await userManager.Users.AnyAsync(u => u.UserName == c.UserName)) continue;

            var courier = new User
            {
                UserName = c.UserName,
                FirstName = c.FirstName,
                LastName = c.LastName,
                PhoneNumber = c.Phone,
                CourierProfile = new CourierProfile { Vehicle = CourierVehicle.Skuter, IsOnShift = true }
            };
            var result = await userManager.CreateAsync(courier, "kurir1234");
            if (result.Succeeded)
                await userManager.AddToRoleAsync(courier, "Courier");
        }

        if (!await context.Markets.AnyAsync())
        {
            var markets = new List<Market>
            {
                new() { Name = "Futoška pijaca", Description = "Najveća novosadska pijaca sa širokim izborom svežeg voća, povrća i domaćih proizvoda.", Address = "Bulevar oslobođenja 30, Novi Sad", ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/9/9a/Futo%C5%A1ka_pijaca_NS_20.jpg/500px-Futo%C5%A1ka_pijaca_NS_20.jpg" },
                new() { Name = "Riblja pijaca", Description = "Tradicionalna pijaca u centru grada, poznata po ribljim specijalitetima i svežem povrću.", Address = "Jevrejska ulica, Novi Sad", ImageUrl = "https://images.unsplash.com/photo-1514425263458-109317cc1321?w=600&h=400&fit=crop" },
                new() { Name = "Limanska pijaca", Description = "Moderna pijaca na Limanu sa odličnom ponudom voća, povrća i začina.", Address = "Narodnog fronta 2, Novi Sad", ImageUrl = "https://images.unsplash.com/photo-1567306295427-94503f8300d7?w=600&h=400&fit=crop" },
                new() { Name = "Najlon pijaca", Description = "Poznata novosadska pijaca sa raznovrsnom ponudom po pristupačnim cenama.", Address = "Temerinski put, Novi Sad", ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/3/37/Najlon_pijaca.JPG/500px-Najlon_pijaca.JPG" },
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
                new() { MarketId = markets[0].Id, StallId = findStall(0, "12").Id, Name = "Marko Voće", Description = "Svežo voće i povrće pravo sa njive iz Šumadije.", Phones = new() { "064 123 4567" } },
                new() { MarketId = markets[0].Id, StallId = findStall(0, "5").Id, Name = "Jovana Organic", Description = "Sertifikovana organska proizvodnja bez pesticida.", Phones = new() { "065 987 6543" } },
                new() { MarketId = markets[0].Id, StallId = findStall(0, "18").Id, Name = "Zeleni Raj", Description = "Začinsko bilje, zeleniš i sezonsko povrće.", Phones = new() { "063 222 3456" } },

                // Riblja pijaca
                new() { MarketId = markets[1].Id, StallId = findStall(1, "7").Id, Name = "Salaš Subotički", Description = "Vojvođanski salaš sa tradicijom dugom 50 godina.", Phones = new() { "069 555 1234" } },
                new() { MarketId = markets[1].Id, StallId = findStall(1, "3").Id, Name = "Bašta Niš", Description = "Južnjačko voće i povrće po najpovoljnijim cenama.", Phones = new() { "062 444 7890" } },

                // Limanska pijaca
                new() { MarketId = markets[2].Id, StallId = findStall(2, "9").Id, Name = "Fruška Gora Bio", Description = "Domaći proizvodi sa Fruške Gore - med, voće, povrće.", Phones = new() { "066 333 2211" } },
                new() { MarketId = markets[2].Id, StallId = findStall(2, "2").Id, Name = "Đurđev Salaš", Description = "Svežo povrće od plastenika do vaše trpeze.", Phones = new() { "064 777 8899" } },

                // Najlon pijaca
                new() { MarketId = markets[3].Id, StallId = findStall(3, "14").Id, Name = "Braća Marković", Description = "Porodična tradicija prodaje voća i povrća od 1985.", Phones = new() { "065 111 4455" } },
                new() { MarketId = markets[3].Id, StallId = findStall(3, "22").Id, Name = "Sremski Plodovi", Description = "Kvalitetno povrće iz srca Srema.", Phones = new() { "069 888 3322" } },
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
                new() { VendorId = vendors[0].Id, Name = "Lubenica", Price = 50, Unit = ProductUnit.Kg, Category = ProductCategory.Voce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1587049352846-4a222e784d38?w=400&h=300&fit=crop" },

                // Jovana Organic (Futoška)
                new() { VendorId = vendors[1].Id, Name = "Organski Paradajz", Price = 350, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1607305387299-a3d9611cd469?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[1].Id, Name = "Organske Tikvice", Price = 280, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1563252722-6434563a985d?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[1].Id, Name = "Organski Krastavci", Price = 250, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/d/df/Basket_with_vegetables_2017_G1.jpg/500px-Basket_with_vegetables_2017_G1.jpg" },
                new() { VendorId = vendors[1].Id, Name = "Organska Rukola", Price = 150, Unit = ProductUnit.Veza, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/4/47/Rucola_F%C3%A4ulnis_in_Packung-1-JS.jpg/500px-Rucola_F%C3%A4ulnis_in_Packung-1-JS.jpg" },
                new() { VendorId = vendors[1].Id, Name = "Bio Cvekla", Price = 180, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1593105544559-ecb03bf76f82?w=400&h=300&fit=crop" },

                // Zeleni Raj (Futoška)
                new() { VendorId = vendors[2].Id, Name = "Bosiljak Sveži", Price = 80, Unit = ProductUnit.Veza, Category = ProductCategory.ZacinskoBilje, IsAvailable = true, ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/5/5c/Fresh_green_basil_leaves_%2854986224214%29.jpg/500px-Fresh_green_basil_leaves_%2854986224214%29.jpg" },
                new() { VendorId = vendors[2].Id, Name = "Peršun", Price = 60, Unit = ProductUnit.Veza, Category = ProductCategory.ZacinskoBilje, IsAvailable = true, ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/b/bd/Parsley_bunches_-_Wellington%2C_New_Zealand.jpg/500px-Parsley_bunches_-_Wellington%2C_New_Zealand.jpg" },
                new() { VendorId = vendors[2].Id, Name = "Mirođija", Price = 60, Unit = ProductUnit.Veza, Category = ProductCategory.ZacinskoBilje, IsAvailable = true, ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/6/60/Fresh_Dill_Leaves.JPG/500px-Fresh_Dill_Leaves.JPG" },
                new() { VendorId = vendors[2].Id, Name = "Zeleni Luk", Price = 50, Unit = ProductUnit.Veza, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/d/da/CSA-Red-Spring-Onions.jpg/500px-CSA-Red-Spring-Onions.jpg" },
                new() { VendorId = vendors[2].Id, Name = "Nana Sveža", Price = 70, Unit = ProductUnit.Veza, Category = ProductCategory.ZacinskoBilje, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1628556270448-4d4e4148e1b1?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[2].Id, Name = "Rotkvice", Price = 100, Unit = ProductUnit.Veza, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/d/d6/A_Bunch_of_Radishes.JPG/500px-A_Bunch_of_Radishes.JPG" },

                // Salaš Subotički (Riblja)
                new() { VendorId = vendors[3].Id, Name = "Crvena Paprika Rog", Price = 220, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/7/77/Spitzpaprika_rot.JPG/500px-Spitzpaprika_rot.JPG" },
                new() { VendorId = vendors[3].Id, Name = "Kupus Zimski", Price = 60, Unit = ProductUnit.Komad, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1594282486552-05b4d80fbb9f?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[3].Id, Name = "Boranija", Price = 300, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Macro_of_Fresh_Green_Beans_Buncis.jpg/500px-Macro_of_Fresh_Green_Beans_Buncis.jpg" },
                new() { VendorId = vendors[3].Id, Name = "Dinja Medena", Price = 120, Unit = ProductUnit.Kg, Category = ProductCategory.Voce, IsAvailable = true, ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/4/4c/Cucumis_melo_var._reticulatus_%28photo_by_Scott_Bauer%29.jpg/500px-Cucumis_melo_var._reticulatus_%28photo_by_Scott_Bauer%29.jpg" },
                new() { VendorId = vendors[3].Id, Name = "Kukuruz Šećerac", Price = 40, Unit = ProductUnit.Komad, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1551754655-cd27e38d2076?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[3].Id, Name = "Breskve", Price = 250, Unit = ProductUnit.Kg, Category = ProductCategory.Voce, IsAvailable = true, ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/a/a5/Peach_fruit_3.jpg/500px-Peach_fruit_3.jpg" },

                // Bašta Niš (Riblja)
                new() { VendorId = vendors[4].Id, Name = "Ljuta Papričica", Price = 400, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/f/f8/VitorJubini_MercadoDaVilaRubim_Vitoria_ES_%2839182859020%29_%28square_crop%29.jpg/500px-VitorJubini_MercadoDaVilaRubim_Vitoria_ES_%2839182859020%29_%28square_crop%29.jpg" },
                new() { VendorId = vendors[4].Id, Name = "Patlidžan", Price = 200, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/4/46/Aubergines_2.jpg/500px-Aubergines_2.jpg" },
                new() { VendorId = vendors[4].Id, Name = "Praziluk", Price = 160, Unit = ProductUnit.Veza, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/3/3f/Leeks_%283388423657%29.jpg/500px-Leeks_%283388423657%29.jpg" },
                new() { VendorId = vendors[4].Id, Name = "Zelena Salata", Price = 80, Unit = ProductUnit.Komad, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1622206151226-18ca2c9ab4a1?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[4].Id, Name = "Spanać", Price = 200, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1576045057995-568f588f82fb?w=400&h=300&fit=crop" },

                // Fruška Gora Bio (Limanska)
                new() { VendorId = vendors[5].Id, Name = "Domaći Med Lipov", Price = 1200, Unit = ProductUnit.Kg, Category = ProductCategory.PcelinjiProizvodi, IsAvailable = true, ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/e/ef/Antiguan_honey_products_at_an_exhibition_in_Saint_Lucia.jpg/500px-Antiguan_honey_products_at_an_exhibition_in_Saint_Lucia.jpg" },
                new() { VendorId = vendors[5].Id, Name = "Trešnje", Price = 350, Unit = ProductUnit.Kg, Category = ProductCategory.Voce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1528821154947-1aa3d1b74941?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[5].Id, Name = "Višnje", Price = 300, Unit = ProductUnit.Kg, Category = ProductCategory.Voce, IsAvailable = true, ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/3/3b/001_Sour_Cherry_Sauce.jpg/500px-001_Sour_Cherry_Sauce.jpg" },
                new() { VendorId = vendors[5].Id, Name = "Maline", Price = 500, Unit = ProductUnit.Kg, Category = ProductCategory.Voce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1577069861033-55d04cec4ef5?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[5].Id, Name = "Kupine", Price = 450, Unit = ProductUnit.Kg, Category = ProductCategory.Voce, IsAvailable = true, ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/c/cc/Blackberries_%28Rubus_fruticosus%29.jpg/500px-Blackberries_%28Rubus_fruticosus%29.jpg" },

                // Đurđev Salaš (Limanska)
                new() { VendorId = vendors[6].Id, Name = "Paradajz Cherry", Price = 300, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/3/39/Cherry-Tomatoes-in-Pack.jpg/500px-Cherry-Tomatoes-in-Pack.jpg" },
                new() { VendorId = vendors[6].Id, Name = "Paprike Šilje", Price = 200, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/2/21/Biber_k%C4%B1zartmas%C4%B1.jpg/500px-Biber_k%C4%B1zartmas%C4%B1.jpg" },
                new() { VendorId = vendors[6].Id, Name = "Krastavci Kornišoni", Price = 180, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/4/40/Polish_style_pickled_cucumbers_IMGP0413.jpg/500px-Polish_style_pickled_cucumbers_IMGP0413.jpg" },
                new() { VendorId = vendors[6].Id, Name = "Tikvice", Price = 150, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1563252722-6434563a985d?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[6].Id, Name = "Šargarepa", Price = 130, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1598170845058-32b9d6a5da37?w=400&h=300&fit=crop" },

                // Braća Marković (Najlon)
                new() { VendorId = vendors[7].Id, Name = "Krompir Crveni", Price = 80, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/b/be/Russet_and_Red_Potatoes_%2835842865704%29.jpg/500px-Russet_and_Red_Potatoes_%2835842865704%29.jpg" },
                new() { VendorId = vendors[7].Id, Name = "Luk Crni", Price = 130, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/2/25/Onion_on_White.JPG/500px-Onion_on_White.JPG" },
                new() { VendorId = vendors[7].Id, Name = "Beli Luk", Price = 800, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/6/61/Garlic_Bulbs_%28Unsplash%29.jpg/500px-Garlic_Bulbs_%28Unsplash%29.jpg" },
                new() { VendorId = vendors[7].Id, Name = "Grašak Svež", Price = 350, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/9/91/Green_pea_pods.jpg/500px-Green_pea_pods.jpg" },
                new() { VendorId = vendors[7].Id, Name = "Pasulj Tetovac", Price = 600, Unit = ProductUnit.Kg, Category = ProductCategory.Mahunarke, IsAvailable = true, ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/9/93/Phaseolus_vulgaris_white_beans%2C_witte_boon.jpg/500px-Phaseolus_vulgaris_white_beans%2C_witte_boon.jpg" },

                // Sremski Plodovi (Najlon)
                new() { VendorId = vendors[8].Id, Name = "Paradajz Volovsko Srce", Price = 280, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1524593166156-312f362cada0?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[8].Id, Name = "Paprike Kalifornija", Price = 250, Unit = ProductUnit.Kg, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://images.unsplash.com/photo-1563565375-f3fdfdbefa83?w=400&h=300&fit=crop" },
                new() { VendorId = vendors[8].Id, Name = "Karfiol", Price = 180, Unit = ProductUnit.Komad, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/b/b2/Brassica_oleracea_var._botrytis_%28Coliflor%29.jpg/500px-Brassica_oleracea_var._botrytis_%28Coliflor%29.jpg" },
                new() { VendorId = vendors[8].Id, Name = "Brokoli", Price = 250, Unit = ProductUnit.Komad, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/4/4f/Basket_of_broccoli_in_Singapore_market.jpg/500px-Basket_of_broccoli_in_Singapore_market.jpg" },
                new() { VendorId = vendors[8].Id, Name = "Kelj", Price = 100, Unit = ProductUnit.Komad, Category = ProductCategory.Povrce, IsAvailable = true, ImageUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/8/8d/Brassica_oleracea_var._sabauda_JdP.jpg/500px-Brassica_oleracea_var._sabauda_JdP.jpg" },
            };

            context.Products.AddRange(products);
            await context.SaveChangesAsync();
        }
    }

    private static async Task EnsureAdmin(UserManager<User> userManager, bool isDevelopment, ILogger logger)
    {
        var adminPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
        var admin = await userManager.Users.FirstOrDefaultAsync(u => u.UserName == "admin");

        if (admin == null)
        {
            var password = isDevelopment ? DefaultAdminPassword : adminPassword;
            if (string.IsNullOrWhiteSpace(password))
            {
                logger.LogWarning("Admin nalog ne postoji, a ADMIN_PASSWORD nije podešen - admin nije napravljen.");
                return;
            }
            admin = new User { UserName = "admin", FirstName = "Super", LastName = "Admin", Email = "admin@pijacaudzepu.rs" };
            var result = await userManager.CreateAsync(admin, password);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "SuperAdmin");
                logger.LogInformation("Admin nalog je napravljen.");
            }
            else
            {
                logger.LogError("Admin nalog nije napravljen: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
            }
            return;
        }

        // U produkciji admin ne sme da ostane sa podrazumevanom lozinkom
        if (isDevelopment || !await userManager.CheckPasswordAsync(admin, DefaultAdminPassword)) return;
        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogCritical("Admin nalog ima podrazumevanu lozinku! Podesite ADMIN_PASSWORD i ponovo pokrenite server.");
            return;
        }
        var token = await userManager.GeneratePasswordResetTokenAsync(admin);
        var reset = await userManager.ResetPasswordAsync(admin, token, adminPassword);
        if (reset.Succeeded)
            logger.LogWarning("Lozinka admin naloga je promenjena na vrednost iz ADMIN_PASSWORD.");
        else
            logger.LogError("Lozinka admina nije promenjena: {Errors}", string.Join(", ", reset.Errors.Select(e => e.Description)));
    }

    // Demo podaci iz ranije verzije seed-a imali su slike koje ne odgovaraju proizvodu ili više ne postoje,
    // a začinsko bilje, pasulj i med su bili u pogrešnoj kategoriji. Ispravka važi i za postojeće baze (Render),
    // ali menja samo red koji još ima staru vrednost - izmene koje je prodavac sam napravio ostaju.
    private static readonly (string Name, string OldUrl, string NewUrl)[] DemoProductImageFixes =
    {
        ("Lubenica", "https://images.unsplash.com/photo-1589984662742-a13a59b2a237?w=400&h=300&fit=crop", "https://images.unsplash.com/photo-1587049352846-4a222e784d38?w=400&h=300&fit=crop"),
        ("Organski Krastavci", "https://images.unsplash.com/photo-1604977042946-1eecc30259b0?w=400&h=300&fit=crop", "https://upload.wikimedia.org/wikipedia/commons/thumb/d/df/Basket_with_vegetables_2017_G1.jpg/500px-Basket_with_vegetables_2017_G1.jpg"),
        ("Organska Rukola", "https://images.unsplash.com/photo-1576045057995-568f588f82fb?w=400&h=300&fit=crop", "https://upload.wikimedia.org/wikipedia/commons/thumb/4/47/Rucola_F%C3%A4ulnis_in_Packung-1-JS.jpg/500px-Rucola_F%C3%A4ulnis_in_Packung-1-JS.jpg"),
        ("Bosiljak Sveži", "https://images.unsplash.com/photo-1618164435735-413d3b066c9a?w=400&h=300&fit=crop", "https://upload.wikimedia.org/wikipedia/commons/thumb/5/5c/Fresh_green_basil_leaves_%2854986224214%29.jpg/500px-Fresh_green_basil_leaves_%2854986224214%29.jpg"),
        ("Peršun", "https://images.unsplash.com/photo-1599629954294-16196fbd23e0?w=400&h=300&fit=crop", "https://upload.wikimedia.org/wikipedia/commons/thumb/b/bd/Parsley_bunches_-_Wellington%2C_New_Zealand.jpg/500px-Parsley_bunches_-_Wellington%2C_New_Zealand.jpg"),
        ("Mirođija", "https://images.unsplash.com/photo-1615485290382-441e4d049cb5?w=400&h=300&fit=crop", "https://upload.wikimedia.org/wikipedia/commons/thumb/6/60/Fresh_Dill_Leaves.JPG/500px-Fresh_Dill_Leaves.JPG"),
        ("Zeleni Luk", "https://images.unsplash.com/photo-1587049352846-4a222e784d38?w=400&h=300&fit=crop", "https://upload.wikimedia.org/wikipedia/commons/thumb/d/da/CSA-Red-Spring-Onions.jpg/500px-CSA-Red-Spring-Onions.jpg"),
        ("Rotkvice", "https://images.unsplash.com/photo-1585502781079-d2e4c2a83e5e?w=400&h=300&fit=crop", "https://upload.wikimedia.org/wikipedia/commons/thumb/d/d6/A_Bunch_of_Radishes.JPG/500px-A_Bunch_of_Radishes.JPG"),
        ("Crvena Paprika Rog", "https://images.unsplash.com/photo-1601648764658-cf37e8c89b70?w=400&h=300&fit=crop", "https://upload.wikimedia.org/wikipedia/commons/thumb/7/77/Spitzpaprika_rot.JPG/500px-Spitzpaprika_rot.JPG"),
        ("Boranija", "https://images.unsplash.com/photo-1567375698348-5d9d5ae10c3a?w=400&h=300&fit=crop", "https://upload.wikimedia.org/wikipedia/commons/thumb/8/83/Macro_of_Fresh_Green_Beans_Buncis.jpg/500px-Macro_of_Fresh_Green_Beans_Buncis.jpg"),
        ("Dinja Medena", "https://images.unsplash.com/photo-1598170845058-32b9d6a5da37?w=400&h=300&fit=crop", "https://upload.wikimedia.org/wikipedia/commons/thumb/4/4c/Cucumis_melo_var._reticulatus_%28photo_by_Scott_Bauer%29.jpg/500px-Cucumis_melo_var._reticulatus_%28photo_by_Scott_Bauer%29.jpg"),
        ("Breskve", "https://images.unsplash.com/photo-1595124216650-1f02fe30bfaa?w=400&h=300&fit=crop", "https://upload.wikimedia.org/wikipedia/commons/thumb/a/a5/Peach_fruit_3.jpg/500px-Peach_fruit_3.jpg"),
        ("Ljuta Papričica", "https://images.unsplash.com/photo-1526470303-82c787d88682?w=400&h=300&fit=crop", "https://upload.wikimedia.org/wikipedia/commons/thumb/f/f8/VitorJubini_MercadoDaVilaRubim_Vitoria_ES_%2839182859020%29_%28square_crop%29.jpg/500px-VitorJubini_MercadoDaVilaRubim_Vitoria_ES_%2839182859020%29_%28square_crop%29.jpg"),
        ("Patlidžan", "https://images.unsplash.com/photo-1615485290382-441e4d049cb5?w=400&h=300&fit=crop", "https://upload.wikimedia.org/wikipedia/commons/thumb/4/46/Aubergines_2.jpg/500px-Aubergines_2.jpg"),
        ("Praziluk", "https://images.unsplash.com/photo-1567539549213-cc1697632146?w=400&h=300&fit=crop", "https://upload.wikimedia.org/wikipedia/commons/thumb/3/3f/Leeks_%283388423657%29.jpg/500px-Leeks_%283388423657%29.jpg"),
        ("Domaći Med Lipov", "https://images.unsplash.com/photo-1587049352846-4a222e784d38?w=400&h=300&fit=crop", "https://upload.wikimedia.org/wikipedia/commons/thumb/e/ef/Antiguan_honey_products_at_an_exhibition_in_Saint_Lucia.jpg/500px-Antiguan_honey_products_at_an_exhibition_in_Saint_Lucia.jpg"),
        ("Višnje", "https://images.unsplash.com/photo-1551829142-d9b8cf2c9232?w=400&h=300&fit=crop", "https://upload.wikimedia.org/wikipedia/commons/thumb/3/3b/001_Sour_Cherry_Sauce.jpg/500px-001_Sour_Cherry_Sauce.jpg"),
        ("Kupine", "https://images.unsplash.com/photo-1615485290382-441e4d049cb5?w=400&h=300&fit=crop", "https://upload.wikimedia.org/wikipedia/commons/thumb/c/cc/Blackberries_%28Rubus_fruticosus%29.jpg/500px-Blackberries_%28Rubus_fruticosus%29.jpg"),
        ("Paradajz Cherry", "https://images.unsplash.com/photo-1661811820259-2575b82101bf?w=400&h=300&fit=crop", "https://upload.wikimedia.org/wikipedia/commons/thumb/3/39/Cherry-Tomatoes-in-Pack.jpg/500px-Cherry-Tomatoes-in-Pack.jpg"),
        ("Paprike Šilje", "https://images.unsplash.com/photo-1526470303-82c787d88682?w=400&h=300&fit=crop", "https://upload.wikimedia.org/wikipedia/commons/thumb/2/21/Biber_k%C4%B1zartmas%C4%B1.jpg/500px-Biber_k%C4%B1zartmas%C4%B1.jpg"),
        ("Krastavci Kornišoni", "https://images.unsplash.com/photo-1604977042946-1eecc30259b0?w=400&h=300&fit=crop", "https://upload.wikimedia.org/wikipedia/commons/thumb/4/40/Polish_style_pickled_cucumbers_IMGP0413.jpg/500px-Polish_style_pickled_cucumbers_IMGP0413.jpg"),
        ("Krompir Crveni", "https://images.unsplash.com/photo-1518977676601-b53f82aba655?w=400&h=300&fit=crop", "https://upload.wikimedia.org/wikipedia/commons/thumb/b/be/Russet_and_Red_Potatoes_%2835842865704%29.jpg/500px-Russet_and_Red_Potatoes_%2835842865704%29.jpg"),
        ("Luk Crni", "https://images.unsplash.com/photo-1587049352846-4a222e784d38?w=400&h=300&fit=crop", "https://upload.wikimedia.org/wikipedia/commons/thumb/2/25/Onion_on_White.JPG/500px-Onion_on_White.JPG"),
        ("Beli Luk", "https://images.unsplash.com/photo-1540148426945-6cf22a6b2571?w=400&h=300&fit=crop", "https://upload.wikimedia.org/wikipedia/commons/thumb/6/61/Garlic_Bulbs_%28Unsplash%29.jpg/500px-Garlic_Bulbs_%28Unsplash%29.jpg"),
        ("Grašak Svež", "https://images.unsplash.com/photo-1563636619-e9143da7973b?w=400&h=300&fit=crop", "https://upload.wikimedia.org/wikipedia/commons/thumb/9/91/Green_pea_pods.jpg/500px-Green_pea_pods.jpg"),
        ("Pasulj Tetovac", "https://images.unsplash.com/photo-1551462147-ff29053bfc14?w=400&h=300&fit=crop", "https://upload.wikimedia.org/wikipedia/commons/thumb/9/93/Phaseolus_vulgaris_white_beans%2C_witte_boon.jpg/500px-Phaseolus_vulgaris_white_beans%2C_witte_boon.jpg"),
        ("Karfiol", "https://images.unsplash.com/photo-1568702846914-96b305d2aaeb?w=400&h=300&fit=crop", "https://upload.wikimedia.org/wikipedia/commons/thumb/b/b2/Brassica_oleracea_var._botrytis_%28Coliflor%29.jpg/500px-Brassica_oleracea_var._botrytis_%28Coliflor%29.jpg"),
        ("Brokoli", "https://images.unsplash.com/photo-1459411552884-841db9b3cc2a?w=400&h=300&fit=crop", "https://upload.wikimedia.org/wikipedia/commons/thumb/4/4f/Basket_of_broccoli_in_Singapore_market.jpg/500px-Basket_of_broccoli_in_Singapore_market.jpg"),
        ("Kelj", "https://images.unsplash.com/photo-1594282486552-05b4d80fbb9f?w=400&h=300&fit=crop", "https://upload.wikimedia.org/wikipedia/commons/thumb/8/8d/Brassica_oleracea_var._sabauda_JdP.jpg/500px-Brassica_oleracea_var._sabauda_JdP.jpg")
    };

    private static readonly (string Name, string OldUrl, string NewUrl)[] DemoMarketImageFixes =
    {
        ("Futoška pijaca", "https://images.unsplash.com/photo-1526399743290-f73cb4022f48?w=600&h=400&fit=crop", "https://upload.wikimedia.org/wikipedia/commons/thumb/9/9a/Futo%C5%A1ka_pijaca_NS_20.jpg/500px-Futo%C5%A1ka_pijaca_NS_20.jpg"),
        ("Najlon pijaca", "https://images.unsplash.com/photo-1686529896385-8a8d581d0225?w=600&h=400&fit=crop", "https://upload.wikimedia.org/wikipedia/commons/thumb/3/37/Najlon_pijaca.JPG/500px-Najlon_pijaca.JPG")
    };

    // Proizvodi napravljeni pre migracije AddProductCategory dobili su kategoriju 0 = Voće, pa je i luk bio "voće".
    // Demo proizvod se vraća u svoju kategoriju ako je još na Voće ili na ranijoj kategoriji iz seed-a (Old).
    private static readonly (string Name, ProductCategory Old, ProductCategory New)[] DemoCategoryFixes =
    {
        ("Paradajz Domaći", ProductCategory.Povrce, ProductCategory.Povrce),
        ("Paprike Babura", ProductCategory.Povrce, ProductCategory.Povrce),
        ("Krompir Mladi", ProductCategory.Povrce, ProductCategory.Povrce),
        ("Organski Paradajz", ProductCategory.Povrce, ProductCategory.Povrce),
        ("Organske Tikvice", ProductCategory.Povrce, ProductCategory.Povrce),
        ("Organski Krastavci", ProductCategory.Povrce, ProductCategory.Povrce),
        ("Organska Rukola", ProductCategory.Povrce, ProductCategory.Povrce),
        ("Bio Cvekla", ProductCategory.Povrce, ProductCategory.Povrce),
        ("Bosiljak Sveži", ProductCategory.Povrce, ProductCategory.ZacinskoBilje),
        ("Peršun", ProductCategory.Povrce, ProductCategory.ZacinskoBilje),
        ("Mirođija", ProductCategory.Povrce, ProductCategory.ZacinskoBilje),
        ("Zeleni Luk", ProductCategory.Povrce, ProductCategory.Povrce),
        ("Nana Sveža", ProductCategory.Povrce, ProductCategory.ZacinskoBilje),
        ("Rotkvice", ProductCategory.Povrce, ProductCategory.Povrce),
        ("Crvena Paprika Rog", ProductCategory.Povrce, ProductCategory.Povrce),
        ("Kupus Zimski", ProductCategory.Povrce, ProductCategory.Povrce),
        ("Boranija", ProductCategory.Povrce, ProductCategory.Povrce),
        ("Kukuruz Šećerac", ProductCategory.Povrce, ProductCategory.Povrce),
        ("Ljuta Papričica", ProductCategory.Povrce, ProductCategory.Povrce),
        ("Patlidžan", ProductCategory.Povrce, ProductCategory.Povrce),
        ("Praziluk", ProductCategory.Povrce, ProductCategory.Povrce),
        ("Zelena Salata", ProductCategory.Povrce, ProductCategory.Povrce),
        ("Spanać", ProductCategory.Povrce, ProductCategory.Povrce),
        ("Domaći Med Lipov", ProductCategory.Ostalo, ProductCategory.PcelinjiProizvodi),
        ("Paradajz Cherry", ProductCategory.Povrce, ProductCategory.Povrce),
        ("Paprike Šilje", ProductCategory.Povrce, ProductCategory.Povrce),
        ("Krastavci Kornišoni", ProductCategory.Povrce, ProductCategory.Povrce),
        ("Tikvice", ProductCategory.Povrce, ProductCategory.Povrce),
        ("Šargarepa", ProductCategory.Povrce, ProductCategory.Povrce),
        ("Krompir Crveni", ProductCategory.Povrce, ProductCategory.Povrce),
        ("Luk Crni", ProductCategory.Povrce, ProductCategory.Povrce),
        ("Beli Luk", ProductCategory.Povrce, ProductCategory.Povrce),
        ("Grašak Svež", ProductCategory.Povrce, ProductCategory.Povrce),
        ("Pasulj Tetovac", ProductCategory.Povrce, ProductCategory.Mahunarke),
        ("Paradajz Volovsko Srce", ProductCategory.Povrce, ProductCategory.Povrce),
        ("Paprike Kalifornija", ProductCategory.Povrce, ProductCategory.Povrce),
        ("Karfiol", ProductCategory.Povrce, ProductCategory.Povrce),
        ("Brokoli", ProductCategory.Povrce, ProductCategory.Povrce),
        ("Kelj", ProductCategory.Povrce, ProductCategory.Povrce)
    };

    private static async Task FixDemoData(DataContext context)
    {
        foreach (var (name, oldUrl, newUrl) in DemoProductImageFixes)
            await context.Products.Where(p => p.Name == name && p.ImageUrl == oldUrl)
                .ExecuteUpdateAsync(set => set.SetProperty(p => p.ImageUrl, newUrl));
        foreach (var (name, oldUrl, newUrl) in DemoMarketImageFixes)
            await context.Markets.Where(m => m.Name == name && m.ImageUrl == oldUrl)
                .ExecuteUpdateAsync(set => set.SetProperty(m => m.ImageUrl, newUrl));
        foreach (var (name, oldCategory, newCategory) in DemoCategoryFixes)
            await context.Products.Where(p => p.Name == name && p.Category != newCategory
                    && (p.Category == ProductCategory.Voce || p.Category == oldCategory))
                .ExecuteUpdateAsync(set => set.SetProperty(p => p.Category, newCategory));
    }

    // Test nalog sa poznatom lozinkom se zaključava zauvek (LockoutEnd = MaxValue označava da ga je zaključao seed)
    private static async Task LockDemoAccounts(UserManager<User> userManager, ILogger logger)
    {
        foreach (var (userName, password) in DemoAccounts)
        {
            var user = await userManager.FindByNameAsync(userName);
            if (user == null || user.LockoutEnd == DateTimeOffset.MaxValue) continue;
            if (!await userManager.CheckPasswordAsync(user, password)) continue;

            await userManager.SetLockoutEnabledAsync(user, true);
            await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            logger.LogWarning("Test nalog {UserName} ima podrazumevanu lozinku i zaključan je (DEMO_ACCOUNTS=true ga ostavlja aktivnim).", userName);
        }
    }

    private static async Task UnlockDemoAccounts(UserManager<User> userManager)
    {
        foreach (var (userName, _) in DemoAccounts)
        {
            var user = await userManager.FindByNameAsync(userName);
            if (user?.LockoutEnd == DateTimeOffset.MaxValue)
                await userManager.SetLockoutEndDateAsync(user, null);
        }
    }
}
