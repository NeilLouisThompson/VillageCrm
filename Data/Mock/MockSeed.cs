using VillageCrm.Models;

namespace VillageCrm.Data.Mock;

/// <summary>Invented sample data. All names and records are made up.</summary>
public static class MockSeed
{
    public static void Populate(MockStore store)
    {
        var now = DateTime.Now;
        var today = DateTime.Today;

        foreach (var (name, role) in new[]
        {
            ("Lindiwe Khumalo", UserRole.Reception), ("Sister Anne Smit", UserRole.Clinical),
            ("Johan Visser", UserRole.Maintenance), ("Pieter Joubert", UserRole.FacilitiesManager),
            ("Zanele Ndlovu", UserRole.Procurement), ("Rina du Toit", UserRole.Finance),
            ("Grace Mahlangu", UserRole.Manager), ("Kevin Pillay", UserRole.SystemAdmin),
            ("Marietjie Botha", UserRole.RestaurantSupervisor), ("Sizwe Khoza", UserRole.RestaurantSupervisor)
        })
        {
            store.Table<StaffMember>().Add(new StaffMember { FullName = name, Role = role });
        }

        var residents = new[]
        {
            NewResident("Margaret Naidoo", "A-01", CareLevel.Independent, ResidentStatus.InVillage, "082 000 0001", "Priya Naidoo (daughter) 082 000 0101", "Low sugar", ContractType.LifeRight),
            NewResident("Peter van der Merwe", "B-03", CareLevel.Assisted, ResidentStatus.InVillage, "082 000 0002", "Anita van der Merwe (wife) 082 000 0102", "Low salt", ContractType.SectionalTitle),
            NewResident("Johanna Botha", "C-12", CareLevel.FrailCare, ResidentStatus.Hospital, "082 000 0003", "Daniel Botha (son) 082 000 0103", "Soft food", ContractType.LifeRight),
            NewResident("Thandi Mokoena", "A-07", CareLevel.Independent, ResidentStatus.Away, "082 000 0004", "Lerato Mokoena (sister) 082 000 0104", "", ContractType.Rental),
            NewResident("Ian Fourie", "D-02", CareLevel.Independent, ResidentStatus.InVillage, "082 000 0005", "Karen Fourie (wife) 082 000 0105", "", ContractType.LifeRight),
            NewResident("Ruth Abrahams", "B-10", CareLevel.Assisted, ResidentStatus.InVillage, "082 000 0006", "Mark Abrahams (son) 082 000 0106", "Soft food", ContractType.LifeRight),
            NewResident("Samuel Dlamini", "F-04", CareLevel.FrailCare, ResidentStatus.InVillage, "082 000 0007", "Nomsa Dlamini (daughter) 082 000 0107", "Diabetic diet", ContractType.LifeRight),
            NewResident("Elsie Pretorius", "A-03", CareLevel.Independent, ResidentStatus.InVillage, "082 000 0008", "Johan Pretorius (son) 082 000 0108", "Diabetic diet", ContractType.SectionalTitle)
        };
        residents[0].Movements.Add(new MovementEntry { When = now.AddDays(-3), Type = MovementType.ReturnedToVillage, Note = "Back from family visit", RecordedBy = "Lindiwe Khumalo" });
        residents[2].Movements.Add(new MovementEntry { When = now.AddDays(-2), Type = MovementType.HospitalAdmission, Note = "Admitted for observation after a fall", RecordedBy = "Sister Anne Smit" });
        residents[3].Movements.Add(new MovementEntry { When = now.AddDays(-5), Type = MovementType.Holiday, Note = "Visiting family in Durban", RecordedBy = "Lindiwe Khumalo" });
        residents[5].WelfareMonitoringConsent = true;
        residents[6].WelfareMonitoringConsent = true;
        foreach (var r in residents)
        {
            store.Table<Resident>().Add(r);
            r.AccountNumber = $"RES{r.Id:0000}";
            // Demo only: every seeded resident's kiosk PIN is 0000.
            r.KioskPinHash = VillageCrm.Services.PinHasher.Hash("0000");
            store.Table<Resident>().Update(r);
        }

        var medical = new[]
        {
            ("Type 2 diabetes", "Metformin 500 mg, twice daily", "None known", ""),
            ("Mild heart failure", "Furosemide 40 mg, mornings", "Penicillin", "Walking stick"),
            ("Post-fall recovery", "Pain relief as prescribed", "None known", "Wheelchair"),
            ("Hypertension", "Amlodipine 5 mg, daily", "None known", ""),
            ("Arthritis in hands", "Ibuprofen as needed", "Sulfa drugs", ""),
            ("Parkinson's disease", "Levodopa, three times daily", "None known", "Walker"),
            ("Dementia (early stage)", "Donepezil 5 mg, nightly", "None known", "Walker"),
            ("Osteoporosis", "Calcium and vitamin D", "None known", "")
        };
        for (var i = 0; i < residents.Length; i++)
        {
            var (conditions, medication, allergies, mobility) = medical[i];
            store.Table<MedicalRecord>().Add(new MedicalRecord
            {
                ResidentId = residents[i].Id, Conditions = conditions, Medication = medication,
                Allergies = allergies, MobilityAids = mobility, AdvanceDirective = i == 6 ? "DNR on file" : ""
            });
        }

        AddJob(store, residents[0], "Plumbing", "Bathroom tap drips at night", JobPriority.Normal, JobStatus.Acknowledged, now.AddDays(-2), now.AddDays(0.5));
        AddJob(store, residents[1], "Electrical", "Passage light not working", JobPriority.Urgent, JobStatus.Logged, now.AddHours(-3), now.AddHours(-1));
        AddJob(store, residents[4], "Garden", "Gate latch is broken", JobPriority.Low, JobStatus.Scheduled, now.AddDays(-4), now.AddDays(3));
        AddJob(store, residents[6], "Security", "Lounge door will not close properly", JobPriority.Urgent, JobStatus.InProgress, now.AddHours(-1), now.AddHours(1));
        AddJob(store, residents[7], "Cleaning", "Extra clean after a spill in the hall", JobPriority.Normal, JobStatus.Done, now.AddDays(-6), now.AddDays(-4));

        store.Table<Notice>().Add(new Notice { Title = "Water off on Tuesday", Body = "The water will be off from 09:00 to 12:00 in blocks A and B while the main valve is replaced.", PostedAt = now.AddDays(-1), ExpiresOn = today.AddDays(5), PostedBy = "Lindiwe Khumalo" });
        store.Table<Notice>().Add(new Notice { Title = "Friday quiz night", Body = "Join us in the lounge at 18:30. Snacks provided.", PostedAt = now.AddDays(-2), ExpiresOn = today.AddDays(7), PostedBy = "Grace Mahlangu" });
        store.Table<Notice>().Add(new Notice { Title = "This week's menu", Body = "The dining room menu is on the board at reception.", PostedAt = now.AddDays(-3), PostedBy = "Lindiwe Khumalo" });

        AddAsset(store, "Lift - Block A", "Lift", "Block A", ("Monthly lift service", Frequency.Monthly, today.AddDays(-2)), ("Annual lift inspection", Frequency.Annual, today.AddDays(40)));
        AddAsset(store, "Standby generator", "Generator", "Plant room", ("Run test under load", Frequency.Weekly, today), ("Full service", Frequency.BiAnnual, today.AddDays(60)));
        AddAsset(store, "Fire extinguishers", "Fire equipment", "All blocks", ("Annual service and tag", Frequency.Annual, today.AddDays(12)));
        AddAsset(store, "Borehole pump", "Pump", "Garden", ("Check pressure and leaks", Frequency.Monthly, today.AddDays(5)));
        AddAsset(store, "Geyser - Frail care", "Geyser", "Frail care", ("Check thermostat and valve", Frequency.Quarterly, today.AddDays(-1)));

        var good = today.AddMonths(8);
        AddContractor(store, "Sparks Electrical CC", "Electrical", "Thabo Molefe", good, good, good, good, good);
        AddContractor(store, "AquaFix Plumbing", "Plumbing", "Lizelle Steyn", today.AddDays(-15), good, good, good, good);
        AddContractor(store, "BuildRight Construction", "Building", "Andre Nel", good, good, good, today.AddDays(20), good);
        AddContractor(store, "GreenLeaf Landscaping", "Garden", "Sipho Zulu", good, good, good, good, good);

        var paving = new Project { Name = "Resurface main walkway", Scope = "Replace uneven paving with non-slip surface", CostCentre = "CC-200", Budget = 180_000 };
        paving.Quotes.Add(new Quote { Number = 1, ContractorId = 3, ContractorName = "BuildRight Construction", Amount = 172_000, ReceivedOn = today.AddDays(-10) });
        paving.Quotes.Add(new Quote { Number = 2, ContractorId = 4, ContractorName = "GreenLeaf Landscaping", Amount = 165_500, ReceivedOn = today.AddDays(-8) });
        paving.Milestones.Add(new Milestone { Name = "Quotes received", DueDate = today.AddDays(7) });
        store.Table<Project>().Add(paving);

        var lighting = new Project { Name = "LED lighting upgrade", Scope = "Replace passage lights with LED", CostCentre = "CC-110", Budget = 60_000, Status = ProjectStatus.InProgress, AppointedContractorId = 1, AppointedContractor = "Sparks Electrical CC", ContractAmount = 54_000, RetentionHeld = 2_000 };
        lighting.Quotes.Add(new Quote { Number = 1, ContractorId = 1, ContractorName = "Sparks Electrical CC", Amount = 54_000, ReceivedOn = today.AddDays(-40) });
        lighting.Quotes.Add(new Quote { Number = 2, ContractorId = 3, ContractorName = "BuildRight Construction", Amount = 61_000, ReceivedOn = today.AddDays(-38) });
        lighting.Quotes.Add(new Quote { Number = 3, ContractorId = 2, ContractorName = "AquaFix Plumbing", Amount = 66_000, ReceivedOn = today.AddDays(-37) });
        lighting.Milestones.Add(new Milestone { Name = "Blocks A and B", DueDate = today.AddDays(-5), Done = true });
        lighting.Milestones.Add(new Milestone { Name = "Blocks C to F", DueDate = today.AddDays(20) });
        lighting.Variations.Add(new Variation { Description = "Add sensor lights at stairwells", Amount = 6_500, Approved = true });
        lighting.Costs.Add(new ProjectCost { Date = today.AddDays(-6), Kind = "Payment certificate", Description = "Certificate 1", Amount = 20_000 });
        store.Table<Project>().Add(lighting);

        AddStock(store, "PL-001", "Tap washer set", "A1", "pack", 3, 5, 30, 18.50m);
        AddStock(store, "PL-002", "Tap cartridge 40 mm", "A1", "each", 6, 4, 15, 145m);
        AddStock(store, "EL-001", "LED globe 9 W", "B2", "each", 40, 20, 100, 32m);
        AddStock(store, "EL-002", "Light switch, single", "B2", "each", 2, 5, 20, 48m);
        AddStock(store, "CL-001", "Floor cleaner 5 L", "C1", "bottle", 8, 4, 20, 89m);
        AddStock(store, "GN-001", "Gate latch, galvanised", "D3", "each", 1, 2, 6, 120m);
        AddStock(store, "BAR-001", "Lager 340 ml", "Bar fridge", "can", 30, 24, 96, 14m, StockStore.Bar);
        AddStock(store, "BAR-002", "House white wine 750 ml", "Bar rack", "bottle", 5, 6, 24, 65m, StockStore.Bar);
        AddStock(store, "BAR-003", "Rooibos tea, 80 bags", "Bar shelf", "box", 9, 4, 12, 38m, StockStore.Bar);
        AddStock(store, "SHP-001", "Brown bread loaf", "Shop shelf", "loaf", 6, 5, 20, 16m, StockStore.Shop);
        AddStock(store, "SHP-002", "Milk 1 L", "Shop fridge", "carton", 3, 6, 24, 19m, StockStore.Shop);
        AddStock(store, "SHP-003", "Postage stamps (10)", "Till", "pack", 12, 5, 30, 55m, StockStore.Shop);

        SeedRestaurant(store, residents, today);

        store.Table<Purchase>().Add(new Purchase
        {
            Supplier = "Village Hardware", Reason = "Restock light switches", RequestedBy = "Zanele Ndlovu", RequestedAt = now.AddDays(-1),
            Status = PurchaseStatus.AwaitingApproval,
            Lines = [new PurchaseLine { StockItemId = 4, Description = "Light switch, single", Quantity = 18, UnitPrice = 48m }]
        });
        store.Table<Purchase>().Add(new Purchase
        {
            Supplier = "CoolAir Services", Reason = "Replace frail care air conditioner", RequestedBy = "Pieter Joubert", RequestedAt = now.AddDays(-2),
            Status = PurchaseStatus.AwaitingApproval,
            Lines = [new PurchaseLine { Description = "Split unit 24 000 BTU, installed", Quantity = 6, UnitPrice = 19_500m }]
        });

        store.Table<Policy>().Add(new Policy
        {
            Title = "Pets in the village", Owner = "Village manager", Audience = PolicyAudience.Residents,
            Versions =
            [
                new PolicyVersion { Number = 1, Status = PolicyStatus.Superseded, Body = "One small pet per unit.", EffectiveDate = today.AddYears(-2), ReviewDate = today.AddYears(-1), ApprovedBy = "Grace Mahlangu" },
                new PolicyVersion { Number = 2, Status = PolicyStatus.Approved, Body = "Residents may keep one small dog or cat per unit with written permission from the village manager. Pets must be on a lead in shared areas. Owners clean up after their pets.", EffectiveDate = today.AddMonths(-11), ReviewDate = today.AddDays(25), ApprovedBy = "Grace Mahlangu" }
            ],
            Acknowledgements = [new PolicyAcknowledgement { VersionNumber = 2, Person = "Margaret Naidoo", At = now.AddDays(-30) }]
        });
        store.Table<Policy>().Add(new Policy
        {
            Title = "Visitors and gate access", Owner = "Village manager", Audience = PolicyAudience.Both,
            Versions = [new PolicyVersion { Number = 1, Status = PolicyStatus.Approved, Body = "Visitors sign in at the gate. Overnight visitors must be booked with reception. Gate codes may not be shared.", EffectiveDate = today.AddMonths(-3), ReviewDate = today.AddMonths(9), ApprovedBy = "Grace Mahlangu" }]
        });
        store.Table<Policy>().Add(new Policy
        {
            Title = "Medicine handling (frail care)", Owner = "Sister Anne Smit", Audience = PolicyAudience.Staff,
            Versions = [new PolicyVersion { Number = 1, Status = PolicyStatus.Draft, Body = "Scheduled medicines are kept in the locked cabinet and recorded in the register at every dose." }]
        });

        // Indicative only. Verify every item and date with the legal advisor, auditor and payroll provider.
        foreach (var (title, law, requirement, frequency, dueInDays, owner, evidence) in new[]
        {
            ("Annual financial statements to residents", "Housing Development Schemes for Retired Persons Act 65 of 1988", "Give audited statements to residents and hold the AGM", Frequency.Annual, 200, "Village manager", "AGM minutes and statements"),
            ("Frail care facility registration renewal", "Older Persons Act 13 of 2006", "Renew registration with Social Development", Frequency.Annual, 90, "Village manager", "Registration certificate"),
            ("POPIA annual review", "POPIA (Act 4 of 2013)", "Review consent records, breach log and Information Officer details", Frequency.Annual, 20, "System admin", "Review report"),
            ("PAIA manual update", "PAIA (Act 2 of 2000)", "Update and publish the PAIA manual", Frequency.Annual, 150, "System admin", "Published manual"),
            ("Health and safety committee meeting", "OHS Act 85 of 1993", "Hold the H&S committee meeting", Frequency.Quarterly, -10, "Village manager", "Signed minutes"),
            ("Lift inspection certificate", "Driven Machinery and Lift Regulations", "Annual lift inspection by competent person", Frequency.Annual, 45, "Facilities manager", "Inspection certificate"),
            ("Electrical certificate of compliance", "Electrical Installation Regulations", "Valid CoC for alterations", Frequency.Once, -3, "Facilities manager", "CoC"),
            ("Fire equipment service", "SANS 10400-T and municipal fire by-laws", "Service and tag all fire equipment", Frequency.Annual, 12, "Facilities manager", "Service certificate"),
            ("Evacuation drill", "Municipal fire by-laws", "Run and record an evacuation drill", Frequency.BiAnnual, 35, "Facilities manager", "Drill report"),
            ("Kitchen certificate of acceptability", "Foodstuffs Act, Regulation R638", "Renew certificate of acceptability", Frequency.Annual, 120, "Village manager", "Certificate"),
            ("SANC registration of nursing staff", "Nursing Act", "Check annual SANC registration of all nurses", Frequency.Annual, 60, "Clinical", "Registration list"),
            ("COIDA letter of good standing", "COIDA", "Renew own letter of good standing", Frequency.Annual, 75, "Finance", "Letter of good standing"),
            ("Monthly PAYE return (EMP201)", "Income Tax Act / SARS", "Submit EMP201 and pay", Frequency.Monthly, 5, "Finance", "SARS submission receipt"),
            ("Employer reconciliation (EMP501)", "Income Tax Act / SARS", "Submit EMP501", Frequency.BiAnnual, 100, "Finance", "SARS submission receipt"),
            ("VAT return (VAT201)", "VAT Act", "Submit VAT201", Frequency.BiAnnual, 25, "Finance", "SARS submission receipt"),
            ("CIPC annual return", "Companies Act", "File annual return with CIPC", Frequency.Annual, 140, "Finance", "CIPC confirmation"),
            ("B-BBEE certificate renewal", "B-BBEE Act", "Renew B-BBEE certificate or affidavit", Frequency.Annual, 180, "Finance", "Certificate")
        })
        {
            store.Table<ComplianceItem>().Add(new ComplianceItem
            {
                Title = title, Law = law, Requirement = requirement, Frequency = frequency, DueDate = today.AddDays(dueInDays),
                Owner = owner, EvidenceRequired = evidence, VerifyWithLegalAdvisor = true
            });
        }
    }

    private static Resident NewResident(string name, string unit, CareLevel care, ResidentStatus status, string phone, string nextOfKin, string dietary, ContractType contract) => new()
    {
        FullName = name, Unit = unit, CareLevel = care, Status = status, Phone = phone, NextOfKin = nextOfKin,
        EmergencyContact = nextOfKin, DietaryFlag = dietary, ContractType = contract,
        OccupationDate = DateTime.Today.AddYears(-3), Doctor = "Dr L. Mostert", PreferredHospital = "Mediclinic (demo)",
        MedicalAid = "Demo Health", DataSharingConsent = dietary != "", DirectoryOptIn = !name.StartsWith('T'), PhotoConsent = true
    };

    private static void AddJob(MockStore store, Resident r, string category, string description, JobPriority priority, JobStatus status, DateTime loggedAt, DateTime slaDue)
    {
        var job = new JobCard
        {
            ResidentId = r.Id, ResidentName = r.FullName, Unit = r.Unit, Category = category, Description = description,
            Priority = priority, Status = status, LoggedAt = loggedAt, SlaDueAt = slaDue
        };
        job.Log(loggedAt, "Logged by resident");
        if (status != JobStatus.Logged)
        {
            job.AcknowledgedBy = "Lindiwe Khumalo";
            job.Log(loggedAt.AddMinutes(20), "Acknowledged by Lindiwe Khumalo");
        }
        if (status >= JobStatus.Scheduled)
        {
            job.ScheduledFor = DateTime.Today.AddDays(status == JobStatus.Scheduled ? 1 : 0);
            job.ScheduledWindow = "08:00 - 12:00";
        }
        if (status == JobStatus.Done)
        {
            job.CompletedAt = loggedAt.AddDays(1);
            job.LabourHours = 1;
            job.Log(loggedAt.AddDays(1), "Done by Johan Visser");
        }
        store.Table<JobCard>().Add(job);
    }

    private static void AddAsset(MockStore store, string name, string category, string location, params (string Task, Frequency Frequency, DateTime NextDue)[] schedules)
    {
        store.Table<Asset>().Add(new Asset
        {
            Name = name, Category = category, Location = location,
            Schedules = schedules.Select(s => new MaintenanceSchedule { Task = s.Task, Frequency = s.Frequency, NextDue = s.NextDue }).ToList()
        });
    }

    private static void AddContractor(MockStore store, string company, string trade, string contact, DateTime coida, DateTime bbbee, DateTime liability, DateTime ohs, DateTime safetyFile)
    {
        store.Table<Contractor>().Add(new Contractor
        {
            Company = company, Trade = trade, ContactPerson = contact, Phone = "011 000 0000",
            Documents =
            [
                new ContractorDocument { Type = ContractorDocType.CoidaLetter, Reference = "COIDA-" + company[..3].ToUpperInvariant(), ExpiresOn = coida },
                new ContractorDocument { Type = ContractorDocType.BbbeeCertificate, Reference = "BEE-" + company[..3].ToUpperInvariant(), ExpiresOn = bbbee },
                new ContractorDocument { Type = ContractorDocType.PublicLiability, Reference = "PL-" + company[..3].ToUpperInvariant(), ExpiresOn = liability },
                new ContractorDocument { Type = ContractorDocType.OhsAgreement, Reference = "S37-" + company[..3].ToUpperInvariant(), ExpiresOn = ohs },
                new ContractorDocument { Type = ContractorDocType.SafetyFile, Reference = "SF-" + company[..3].ToUpperInvariant(), ExpiresOn = safetyFile }
            ]
        });
    }

    private static void AddStock(MockStore store, string code, string name, string bin, string unit, decimal onHand, decimal min, decimal max, decimal cost, StockStore storeType = StockStore.Maintenance) =>
        store.Table<StockItem>().Add(new StockItem { Store = storeType, Code = code, Name = name, Bin = bin, Unit = unit, OnHand = onHand, Min = min, Max = max, UnitCost = cost });

    private static void SeedRestaurant(MockStore store, Resident[] residents, DateTime today)
    {
        var breakfast = store.Table<Sitting>().Add(new Sitting { Name = "Breakfast", StartsAt = new TimeOnly(7, 30), Capacity = 60, CutOffMinutes = 720, Price = 45m });
        var lunch = store.Table<Sitting>().Add(new Sitting { Name = "Lunch", StartsAt = new TimeOnly(12, 30), Capacity = 80, CutOffMinutes = 150, Price = 85m });
        var dinner = store.Table<Sitting>().Add(new Sitting { Name = "Dinner", StartsAt = new TimeOnly(17, 30), Capacity = 60, CutOffMinutes = 240, Price = 75m });
        var christmas = new DateTime(today.Month == 12 && today.Day > 25 ? today.Year + 1 : today.Year, 12, 25);
        store.Table<Sitting>().Add(new Sitting
        {
            Name = "Christmas lunch", Description = "Festive lunch in the hall with carols. Family welcome.",
            StartsAt = new TimeOnly(12, 0), Capacity = 120, CutOffMinutes = 7 * 24 * 60, Price = 350m, EventDate = christmas
        });

        string[] lunches = ["Roast chicken, potatoes, green beans", "Bobotie with yellow rice", "Fish and chips with salad", "Beef stew with mash", "Vegetable lasagne", "Lamb curry with rice", "Cottage pie with peas"];
        string[] dinners = ["Soup and sandwiches", "Chicken pie and salad", "Omelette and toast", "Macaroni cheese", "Fish cakes and vegetables", "Shepherd's pie", "Quiche and salad"];
        for (var d = -7; d <= 13; d++)
        {
            var date = today.AddDays(d);
            var i = (d + 70) % 7;
            store.Table<MenuEntry>().Add(new MenuEntry { Date = date, SittingId = breakfast.Id, Dishes = "Oats, eggs, toast, fruit", DietaryOptions = "Gluten-free toast, soft food" });
            store.Table<MenuEntry>().Add(new MenuEntry { Date = date, SittingId = lunch.Id, Dishes = lunches[i], DietaryOptions = "Diabetic dessert, soft food, vegetarian" });
            store.Table<MenuEntry>().Add(new MenuEntry { Date = date, SittingId = dinner.Id, Dishes = dinners[i], DietaryOptions = "Soft food, low salt" });
        }
        store.Table<MenuEntry>().Add(new MenuEntry { Date = christmas, SittingId = store.Table<Sitting>().List(s => s.EventDate != null).Single().Id, Dishes = "Gammon, roast turkey, trimmings, Christmas pudding", DietaryOptions = "Diabetic pudding, soft food, vegetarian" });

        // Recent lunches: most residents attended; Samuel (welfare consent) last ate 4 days ago, so he raises an alert.
        foreach (var (resident, daysAgo) in new[] { (0, 1), (0, 3), (1, 1), (4, 2), (5, 1), (5, 2), (6, 4), (7, 1), (7, 2) })
        {
            var r = residents[resident];
            store.Table<MealBooking>().Add(new MealBooking
            {
                ResidentId = r.Id, ResidentName = r.FullName, Unit = r.Unit, Date = today.AddDays(-daysAgo),
                SittingId = lunch.Id, SittingName = lunch.Name, Status = BookingStatus.Attended,
                BookedBy = r.FullName, BookedAt = today.AddDays(-daysAgo - 1), Guests = daysAgo == 3 ? 1 : 0
            });
        }
        foreach (var resident in new[] { 0, 1, 4, 5, 7 })
        {
            var r = residents[resident];
            store.Table<MealBooking>().Add(new MealBooking
            {
                ResidentId = r.Id, ResidentName = r.FullName, Unit = r.Unit, Date = today.AddDays(1),
                SittingId = lunch.Id, SittingName = lunch.Name, Status = BookingStatus.Booked,
                BookedBy = r.FullName, BookedAt = today
            });
        }
    }
}
