#!/usr/bin/env python3
"""
Comprehensive data entry script for PTG Oil System
Loads all Excel data into the PostgreSQL database.
"""
import os, sys, openpyxl, psycopg2
from datetime import datetime, timezone

DATABASE_URL = os.environ.get("DATABASE_URL", "")
conn = psycopg2.connect(DATABASE_URL)
cur = conn.cursor()
NOW = "NOW()"

def q(val):
    if val is None:
        return "NULL"
    if isinstance(val, bool):
        return "TRUE" if val else "FALSE"
    if isinstance(val, (int, float)):
        return str(val)
    if isinstance(val, datetime):
        return f"'{val.strftime('%Y-%m-%d %H:%M:%S+00')}'"
    s = str(val).replace("'", "''")
    return f"'{s}'"

def insert(table, data):
    cols = ", ".join(f'"{k}"' for k in data.keys())
    vals = ", ".join(q(v) for v in data.values())
    cur.execute(f'INSERT INTO "{table}" ({cols}) VALUES ({vals}) RETURNING "Id"')
    return cur.fetchone()[0]

def upsert_returning(table, check_col, check_val, data):
    cur.execute(f'SELECT "Id" FROM "{table}" WHERE "{check_col}" = %s LIMIT 1', (check_val,))
    row = cur.fetchone()
    if row:
        return row[0]
    return insert(table, data)

print("=== PTG Oil System Data Entry ===")
print()

# ──────────────────────────────────────────────
# 1. COMPANIES
# ──────────────────────────────────────────────
print("1. Creating master data...")

# Existing: sedegi(1), solvix(2)
# Create: PTG, BNK, Fawad Coltd, Mozamil
co_ptg = upsert_returning("Companies", "Code", "PTG", {
    "Code": "PTG", "Name": "PETROGAZ TRADING", "NamePersian": "پترو گز تریدینگ",
    "Country": "AE", "IsActive": True, "CreatedAtUtc": datetime.utcnow()
})
co_bnk = upsert_returning("Companies", "Code", "BNK", {
    "Code": "BNK", "Name": "BNK", "NamePersian": "BNK",
    "Country": "BY", "IsActive": True, "CreatedAtUtc": datetime.utcnow()
})
co_fawad = upsert_returning("Companies", "Code", "FAWAD", {
    "Code": "FAWAD", "Name": "Fawad Coltd", "NamePersian": "فواد شرکت",
    "Country": "AF", "IsActive": True, "CreatedAtUtc": datetime.utcnow()
})
co_moz = upsert_returning("Companies", "Code", "MOZ", {
    "Code": "MOZ", "Name": "Mozamil Enerji", "NamePersian": "مزمل انرجی",
    "Country": "TR", "IsActive": True, "CreatedAtUtc": datetime.utcnow()
})
co_solvex = 2  # existing solvix
co_sedegi = 1  # existing sedegi = main PTG company
print(f"  Companies: PTG={co_ptg}, BNK={co_bnk}, Fawad={co_fawad}, Moz={co_moz}")

# ──────────────────────────────────────────────
# 2. PRODUCTS
# ──────────────────────────────────────────────
prod_petrol = 1  # existing پطرول = AI-92-K5

cur.execute('SELECT "Id" FROM "Products" WHERE "Code" = %s LIMIT 1', ('LPG',))
row = cur.fetchone()
if row:
    prod_lpg = row[0]
else:
    prod_lpg = insert("Products", {
        "Code": "LPG", "Name": "LPG", "NamePersian": "گاز مایع",
        "UnitOfMeasure": "MT", "IsActive": True,
        "CreatedAtUtc": datetime.utcnow()
    })
print(f"  Products: Petrol={prod_petrol}, LPG={prod_lpg}")

# ──────────────────────────────────────────────
# 3. SUPPLIERS
# ──────────────────────────────────────────────
sup_solvex = 1  # existing solvix

cur.execute('SELECT "Id" FROM "Suppliers" WHERE "Name" = %s LIMIT 1', ('BNK',))
row = cur.fetchone()
sup_bnk = row[0] if row else insert("Suppliers", {
    "Name": "BNK", "NamePersian": "BNK", "Country": "BY",
    "IsActive": True, "CreatedAtUtc": datetime.utcnow()
})

cur.execute('SELECT "Id" FROM "Suppliers" WHERE "Name" = %s LIMIT 1', ('Mozamil Enerji',))
row = cur.fetchone()
sup_moz = row[0] if row else insert("Suppliers", {
    "Name": "Mozamil Enerji", "NamePersian": "مزمل انرجی", "Country": "TR",
    "IsActive": True, "CreatedAtUtc": datetime.utcnow()
})
print(f"  Suppliers: SOLVEX={sup_solvex}, BNK={sup_bnk}, Mozamil={sup_moz}")

# ──────────────────────────────────────────────
# 4. CUSTOMERS
# ──────────────────────────────────────────────
def get_or_create_customer(name, name_persian, country):
    cur.execute('SELECT "Id" FROM "Customers" WHERE "Name" = %s LIMIT 1', (name,))
    row = cur.fetchone()
    if row:
        return row[0]
    return insert("Customers", {
        "Name": name, "NamePersian": name_persian, "Country": country,
        "IsActive": True, "CreatedAtUtc": datetime.utcnow()
    })

cust_bnk       = get_or_create_customer("BNK", "BNK", "BY")
cust_fawad     = get_or_create_customer("Fawad Coltd", "فواد شرکت", "AF")
cust_aziz      = get_or_create_customer("Aziz Ahmad Sultan Husain", "عزیز احمد سلطان حسین", "AF")
cust_sadeq     = get_or_create_customer("Sadeq Samangani", "صادق سمنگانی", "AF")
cust_solvex    = get_or_create_customer("SOLVEX FZE", "سولویکس", "AE")
print(f"  Customers: BNK={cust_bnk}, Fawad={cust_fawad}, Aziz={cust_aziz}, Sadeq={cust_sadeq}, Solvex={cust_solvex}")

# ──────────────────────────────────────────────
# 5. LOCATIONS
# ──────────────────────────────────────────────
def get_or_create_location(name, name_persian, country, kind):
    cur.execute('SELECT "Id" FROM "Locations" WHERE "Name" = %s LIMIT 1', (name,))
    row = cur.fetchone()
    if row:
        return row[0]
    return insert("Locations", {
        "Name": name, "NamePersian": name_persian, "Country": country,
        "Kind": kind, "CreatedAtUtc": datetime.utcnow()
    })

loc_trusowo     = get_or_create_location("Trusowo", "تروسوو", "RU", "Terminal")
loc_novopol     = get_or_create_location("Novopolotsk", "نووپولوتسک", "BY", "Terminal")
loc_akina       = get_or_create_location("Akina", "آقینه", "AF", "City")
loc_herat       = get_or_create_location("Herat", "هرات", "AF", "City")
loc_okarem      = get_or_create_location("Okarem", "اکریم", "TM", "Port")
loc_turkmn      = get_or_create_location("Turkmanbashi", "ترکمن باشی", "TM", "Port")
print(f"  Locations: Trusowo={loc_trusowo}, Novopolotsk={loc_novopol}, Akina={loc_akina}, Herat={loc_herat}, Okarem={loc_okarem}")

# ──────────────────────────────────────────────
# 6. VESSELS
# ──────────────────────────────────────────────
cur.execute('SELECT "Id" FROM "Vessels" WHERE "Name" = %s LIMIT 1', ('Volga',))
row = cur.fetchone()
vessel_volga = row[0] if row else insert("Vessels", {
    "Name": "Volga", "IsActive": True, "CreatedAtUtc": datetime.utcnow()
})
print(f"  Vessel Volga={vessel_volga}")

# ──────────────────────────────────────────────
# 7. CONTRACTS
# ──────────────────────────────────────────────
def get_or_create_contract(number, data):
    cur.execute('SELECT "Id" FROM "Contracts" WHERE "ContractNumber" = %s LIMIT 1', (number,))
    row = cur.fetchone()
    if row:
        return row[0]
    return insert("Contracts", data)

# PTG-SOL-25-MOZ AA#6: PTG (sedegi=1) buys from SOLVEX (sup_solvex=1), 10000 MT AI-92 @ $570
# ContractType: 1=Purchase, 2=Sale ; Status: 1=Active ; PricingMethod: 0=Fixed
c_ptg_sol_moz = get_or_create_contract("PTG-SOL-25-MOZ-AA6", {
    "ContractNumber": "PTG-SOL-25-MOZ-AA6",
    "ContractType": 1,  # Purchase
    "Status": 1,
    "CompanyId": co_sedegi,
    "ProductId": prod_petrol,
    "SupplierId": sup_solvex,
    "DestinationLocationId": loc_trusowo,
    "ContractDate": datetime(2026, 1, 14),
    "StartDate": datetime(2026, 1, 19),
    "PricingMethod": 0,  # Fixed
    "QuantityMt": 10000,
    "UnitPriceUsd": 570,
    "Currency": "USD",
    "Notes": "Additional Agreement #6 dd 14.01.2026, PETROGAZ TRADING - SOLVEX FZE, SPT TRUSOVO",
    "OwnershipType": 1,
    "CreatedAtUtc": datetime.utcnow()
})

# SOLVEX-BNK 8-4-11/93 AA#8.93-H: SOLVEX sells to BNK, 9600 MT, Jan 2026, Novopolotsk, Platts-based
c_bnk_sol_jan = get_or_create_contract("8-4-11-93-AA8-H", {
    "ContractNumber": "8-4-11-93-AA8-H",
    "ContractType": 2,  # Sale
    "Status": 1,
    "CompanyId": co_solvex,
    "ProductId": prod_petrol,
    "CustomerId": cust_bnk,
    "DestinationLocationId": loc_novopol,
    "ContractDate": datetime(2025, 12, 19),
    "StartDate": datetime(2026, 1, 6),
    "PricingMethod": 1,  # Platts
    "QuantityMt": 9600,
    "PremiumDiscountUsd": -125.41,
    "BenchmarkCode": "Platts",
    "Currency": "USD",
    "Notes": "Contract 8-4-11/93 AA#8.93-H dd 19.12.2025, SOLVEX-BNK, FCA ST.NOVOPOLOTSK, 9600 mt",
    "OwnershipType": 1,
    "CreatedAtUtc": datetime.utcnow()
})

# SOLVEX-BNK 8-4-11/93 AA#10.93-M: 10000 MT, Mar 2026, Trusovo, Platts-based
c_bnk_sol_mar = get_or_create_contract("8-4-11-93-AA10-M", {
    "ContractNumber": "8-4-11-93-AA10-M",
    "ContractType": 2,
    "Status": 1,
    "CompanyId": co_solvex,
    "ProductId": prod_petrol,
    "CustomerId": cust_bnk,
    "DestinationLocationId": loc_trusowo,
    "ContractDate": datetime(2026, 2, 20),
    "StartDate": datetime(2026, 3, 7),
    "PricingMethod": 1,
    "QuantityMt": 10000,
    "PremiumDiscountUsd": -42,
    "BenchmarkCode": "Platts",
    "Currency": "USD",
    "Notes": "Contract 8-4-11/93 AA#10.93-M dd 20.02.2026, SOLVEX-BNK, CPT TRUSOVO-EKSPERT, 10000 mt",
    "OwnershipType": 1,
    "CreatedAtUtc": datetime.utcnow()
})

# LPG b-034321: PTG buys from Mozamil Enerji, 1000 MT LPG, Nov 2022, Akina
c_lpg = get_or_create_contract("b-034321", {
    "ContractNumber": "b-034321",
    "ContractType": 1,
    "Status": 2,  # Completed
    "CompanyId": co_sedegi,
    "ProductId": prod_lpg,
    "SupplierId": sup_moz,
    "DestinationLocationId": loc_akina,
    "ContractDate": datetime(2022, 11, 15),
    "StartDate": datetime(2022, 11, 25),
    "EndDate": datetime(2023, 3, 8),
    "PricingMethod": 1,
    "QuantityMt": 1000,
    "PremiumDiscountUsd": -170,
    "BenchmarkCode": "Platts",
    "MinimumPriceUsd": 310,
    "Currency": "USD",
    "Notes": "Contract b-034321 dd 15.11.2022, Mozamil Enerji, LPG to Akina, FCA NAIP terminal",
    "OwnershipType": 1,
    "CreatedAtUtc": datetime.utcnow()
})

# VOLGA-BNK: PTG buys from BNK-SOLVEX, 2350 MT AI-92, Feb 2026
c_volga_bnk = get_or_create_contract("VOLGA-BNK-2026", {
    "ContractNumber": "VOLGA-BNK-2026",
    "ContractType": 1,
    "Status": 2,
    "CompanyId": co_sedegi,
    "ProductId": prod_petrol,
    "SupplierId": sup_bnk,
    "DestinationLocationId": loc_herat,
    "ContractDate": datetime(2026, 2, 1),
    "StartDate": datetime(2026, 2, 1),
    "PricingMethod": 0,
    "QuantityMt": 2350,
    "UnitPriceUsd": 600.73,
    "Currency": "USD",
    "Notes": "Vessel VOLGA: purchase from BNK-SOLVEX, 2350 MT AI-92-K5 @ $600.73/MT via Okarem",
    "OwnershipType": 1,
    "CreatedAtUtc": datetime.utcnow()
})

# VOLGA-PTG: PTG buys from SOLVEX, 1556.531 MT AI-92, Feb 2026
c_volga_ptg = get_or_create_contract("VOLGA-PTG-SOL-2026", {
    "ContractNumber": "VOLGA-PTG-SOL-2026",
    "ContractType": 1,
    "Status": 2,
    "CompanyId": co_sedegi,
    "ProductId": prod_petrol,
    "SupplierId": sup_solvex,
    "DestinationLocationId": loc_herat,
    "ContractDate": datetime(2026, 2, 1),
    "StartDate": datetime(2026, 2, 1),
    "PricingMethod": 0,
    "QuantityMt": 1556.531,
    "UnitPriceUsd": 570,
    "Currency": "USD",
    "Notes": "Vessel VOLGA: purchase from PETROGAS/SOLVEX, 1556.531 MT AI-92-K5 @ $570/MT via Okarem",
    "OwnershipType": 1,
    "CreatedAtUtc": datetime.utcnow()
})

print(f"  Contracts: PTG-SOL-MOZ={c_ptg_sol_moz}, BNK-Jan={c_bnk_sol_jan}, BNK-Mar={c_bnk_sol_mar}, LPG={c_lpg}, VOLGA-BNK={c_volga_bnk}, VOLGA-PTG={c_volga_ptg}")

conn.commit()

# ──────────────────────────────────────────────
# 8. LOADING REGISTERS — PTG-SOL-MOZ (89 wagons)
# ──────────────────────────────────────────────
print()
print("2. Loading PTG-SOL-MOZ wagons (89)...")

wb_ptg = openpyxl.load_workbook('attached_assets/Account_and_Loading_PTG-SOL-MOZ_2026_1777772526489.xlsx', data_only=True)
ws_ptg = wb_ptg['5000 MT 570$']
rows_ptg = list(ws_ptg.iter_rows(values_only=True))
ptg_wagons = [r for r in rows_ptg[3:] if r[0] is not None and isinstance(r[0], int)]

ptg_register_ids = []
for r in ptg_wagons:
    no, date, rwb, wagon, loaded_mt, price, total, consignee, dest = r[0], r[1], r[2], r[3], r[4], r[5], r[6], r[7], r[8]
    rwb_clean = str(rwb).replace('\n', ' ').strip() if rwb else None
    rid = insert("LoadingRegisters", {
        "ContractId": c_ptg_sol_moz,
        "ProductId": prod_petrol,
        "OriginLocationId": loc_okarem,
        "LoadingDate": date if date else datetime(2026, 1, 19),
        "LoadedQuantityMt": round(float(loaded_mt), 4),
        "LoadingPriceUsd": round(float(price), 4),
        "WagonNumber": str(wagon),
        "RwbNo": rwb_clean,
        "ConsigneeName": str(consignee) if consignee else None,
        "DestinationName": str(dest) if dest else None,
        "TransportType": 1,  # Rail
        "BillOfLadingNumber": rwb_clean,
        "Notes": f"PTG-SOL-25-MOZ AA#6, Wagon #{no}",
        "CreatedAtUtc": datetime.utcnow()
    })
    ptg_register_ids.append(rid)

print(f"  Inserted {len(ptg_register_ids)} PTG-SOL-MOZ wagon registers")

# Loading Receipts for PTG-SOL-MOZ (arrival at Terminal Ilinka = terminal 1)
terminal_ilinka = 1
receipt_count_ptg = 0
for i, r in enumerate(ptg_wagons):
    no, date, rwb, wagon, loaded_mt = r[0], r[1], r[2], r[3], r[4]
    insert("LoadingReceipts", {
        "LoadingRegisterId": ptg_register_ids[i],
        "TerminalId": terminal_ilinka,
        "ReceiptDate": date if date else datetime(2026, 1, 19),
        "ReceivedQuantityMt": round(float(loaded_mt), 4),
        "ReferenceDocument": str(rwb).replace('\n', ' ').strip() if rwb else None,
        "Notes": f"Receipt PTG-SOL-MOZ wagon {wagon}",
        "CreatedAtUtc": datetime.utcnow()
    })
    receipt_count_ptg += 1

print(f"  Inserted {receipt_count_ptg} PTG-SOL-MOZ loading receipts")
conn.commit()

# ──────────────────────────────────────────────
# 9. LOADING REGISTERS — BNK-SOL January (164 wagons)
# ──────────────────────────────────────────────
print()
print("3. Loading BNK-SOL January wagons (164)...")

wb_bnk = openpyxl.load_workbook('attached_assets/ACCOUNT-LOADING_SOLVEX-BNK_2026_(1)_1777772526490.xlsx', data_only=True)
ws_jan = wb_bnk['9600 MT January']
rows_jan = list(ws_jan.iter_rows(values_only=True))
jan_wagons = [r for r in rows_jan[4:] if r[0] is not None and isinstance(r[0], int)]

jan_register_ids = []
for r in jan_wagons:
    no = r[0]; date = r[1]; rwb = r[2]; wagon = r[3]; loaded_mt = r[4]
    platts = r[5]; discount = r[6]; total_usd = r[7]; price_rub = r[8]; total_rub = r[9]
    consignee = r[10]; dest = r[11]
    arr_date = r[13]; leak_date = r[14]; actual_qty = r[15]; diff = r[16]

    net_price = float(discount) if discount and isinstance(discount, (int, float)) else None
    rid = insert("LoadingRegisters", {
        "ContractId": c_bnk_sol_jan,
        "ProductId": prod_petrol,
        "LoadingDate": date if date else datetime(2026, 1, 6),
        "LoadedQuantityMt": round(float(loaded_mt), 4),
        "LoadingPriceUsd": round(net_price, 4) if net_price else None,
        "PlattsUsd": round(float(platts), 4) if platts and isinstance(platts, (int, float)) else None,
        "WagonNumber": str(wagon),
        "RwbNo": str(rwb) if rwb else None,
        "ConsigneeName": str(consignee) if consignee else None,
        "DestinationName": str(dest) if dest else None,
        "TransportType": 1,
        "Notes": f"8-4-11/93 AA#8.93-H January, Wagon #{no}",
        "CreatedAtUtc": datetime.utcnow()
    })
    jan_register_ids.append((rid, arr_date, leak_date, actual_qty, loaded_mt, date))

# Loading Receipts (only for those with arrival or leak data)
# Col 14 = LeakDate, col 15 = ActualQty
receipt_jan = 0
for rid, arr_date, leak_date, actual_qty, loaded_mt, load_date in jan_register_ids:
    receipt_date = leak_date or load_date or datetime(2026, 1, 12)
    received_qty = float(actual_qty) if actual_qty and isinstance(actual_qty, (int, float)) else float(loaded_mt)
    insert("LoadingReceipts", {
        "LoadingRegisterId": rid,
        "TerminalId": terminal_ilinka,
        "ReceiptDate": receipt_date,
        "ReceivedQuantityMt": round(received_qty, 4),
        "ArrivalDate": None,
        "LeakDate": leak_date,
        "ActualArrivedQuantityMt": round(float(actual_qty), 4) if actual_qty and isinstance(actual_qty, (int, float)) else None,
        "Notes": "BNK-SOL January receipt",
        "CreatedAtUtc": datetime.utcnow()
    })
    receipt_jan += 1

print(f"  Inserted {len(jan_register_ids)} BNK-SOL Jan wagons, {receipt_jan} receipts")
conn.commit()

# ──────────────────────────────────────────────
# 10. LOADING REGISTERS — BNK-SOL March (166 wagons)
# ──────────────────────────────────────────────
print()
print("4. Loading BNK-SOL March wagons (166)...")

ws_mar = wb_bnk['10000 MT MARCH ']
rows_mar = list(ws_mar.iter_rows(values_only=True))
mar_wagons = [r for r in rows_mar[4:] if r[0] is not None and isinstance(r[0], int)]

mar_register_ids = []
for r in mar_wagons:
    no = r[0]; date_raw = r[1]; rwb = r[2]; wagon = r[3]; loaded_mt = r[4]
    platts = r[5]; discount = r[6]; total_usd = r[7]; price_rub = r[8]; total_rub = r[9]
    consignee = r[10]; dest = r[11]
    arr_date = r[13]; leak_date = r[14]; actual_qty = r[15]

    # date might be string '07.03.2026'
    if isinstance(date_raw, datetime):
        load_date = date_raw
    elif isinstance(date_raw, str):
        try:
            load_date = datetime.strptime(date_raw.strip(), '%d.%m.%Y')
        except:
            load_date = datetime(2026, 3, 7)
    else:
        load_date = datetime(2026, 3, 7)

    net_price = None
    if discount and isinstance(discount, (int, float)):
        net_price = round(float(discount), 4)
    elif total_usd and isinstance(total_usd, str) and total_usd != '#REF!':
        net_price = None

    rid = insert("LoadingRegisters", {
        "ContractId": c_bnk_sol_mar,
        "ProductId": prod_petrol,
        "LoadingDate": load_date,
        "LoadedQuantityMt": round(float(loaded_mt), 4) if isinstance(loaded_mt, (int, float)) else 0,
        "LoadingPriceUsd": net_price,
        "PlattsUsd": round(float(platts), 4) if platts and isinstance(platts, (int, float)) else None,
        "WagonNumber": str(wagon) if wagon else None,
        "RwbNo": str(rwb).strip() if rwb else None,
        "ConsigneeName": str(consignee) if consignee else None,
        "DestinationName": str(dest) if dest else None,
        "TransportType": 1,
        "Notes": f"8-4-11/93 AA#10.93-M March, Wagon #{no}",
        "CreatedAtUtc": datetime.utcnow()
    })
    mar_register_ids.append((rid, leak_date, actual_qty, loaded_mt, load_date))

# Receipts
for rid, leak_date, actual_qty, loaded_mt, load_date in mar_register_ids:
    receipt_date = leak_date or load_date or datetime(2026, 3, 7)
    received_qty = float(actual_qty) if actual_qty and isinstance(actual_qty, (int, float)) else (float(loaded_mt) if isinstance(loaded_mt, (int, float)) else 0)
    insert("LoadingReceipts", {
        "LoadingRegisterId": rid,
        "TerminalId": terminal_ilinka,
        "ReceiptDate": receipt_date,
        "ReceivedQuantityMt": round(received_qty, 4),
        "LeakDate": leak_date,
        "ActualArrivedQuantityMt": round(float(actual_qty), 4) if actual_qty and isinstance(actual_qty, (int, float)) else None,
        "Notes": "BNK-SOL March receipt",
        "CreatedAtUtc": datetime.utcnow()
    })

print(f"  Inserted {len(mar_register_ids)} BNK-SOL Mar wagons + receipts")
conn.commit()

# ──────────────────────────────────────────────
# 11. LOADING REGISTERS — LPG b-034321 (28 wagons)
# ──────────────────────────────────────────────
print()
print("5. Loading LPG b-034321 wagons (28)...")

wb_lpg = openpyxl.load_workbook('attached_assets/b-034321_-_LPG_1777772526491.xlsx', data_only=True)
ws_lpg_load = wb_lpg['loading']
rows_lpg = list(ws_lpg_load.iter_rows(values_only=True))
lpg_wagons = [r for r in rows_lpg[4:] if r[0] is not None and isinstance(r[0], int)]

# Build RW expenses lookup by wagon number
ws_rwe = wb_lpg['Rw Expenses ']
rows_rwe = list(ws_rwe.iter_rows(values_only=True))
rwe_by_wagon = {}
for r in rows_rwe[1:]:
    if r[0] is not None and isinstance(r[0], int) and r[3]:
        wagon_no = str(r[3])
        chargeable_mt = float(r[5]) if r[5] and isinstance(r[5], (int, float)) else None
        rate = float(r[7]) if r[7] and isinstance(r[7], (int, float)) else None
        expense = float(r[8]) if r[8] and isinstance(r[8], (int, float)) else None
        rwe_by_wagon[wagon_no] = (chargeable_mt, rate, expense)

lpg_register_ids = []
for r in lpg_wagons:
    no = r[0]; date = r[1]; rwb = r[2]; wagon = r[3]; loaded_mt = r[4]
    platts = r[5]; price = r[6]; amount = r[7]; consignee = r[8]; transport_co = r[9]; dest = r[10]

    wagon_str = str(wagon)
    rwe = rwe_by_wagon.get(wagon_str, (None, None, None))
    chargeable_mt, rw_rate, rw_expense = rwe

    rid = insert("LoadingRegisters", {
        "ContractId": c_lpg,
        "ProductId": prod_lpg,
        "OriginLocationId": loc_turkmn,
        "LoadingDate": date if date else datetime(2022, 11, 25),
        "LoadedQuantityMt": round(float(loaded_mt), 4),
        "LoadingPriceUsd": round(float(price), 4) if isinstance(price, (int, float)) else None,
        "PlattsUsd": round(float(platts), 4) if isinstance(platts, (int, float)) else None,
        "WagonNumber": wagon_str,
        "RwbNo": str(rwb) if rwb else None,
        "ConsigneeName": str(consignee) if consignee else None,
        "DestinationName": str(dest) if dest else None,
        "LogisticsCompanyName": str(transport_co) if transport_co else None,
        "TransportType": 1,
        "ChargeableQuantityMt": round(chargeable_mt, 4) if chargeable_mt else None,
        "RailwayRateUsd": round(rw_rate, 4) if rw_rate else None,
        "RailwayExpenseUsd": round(rw_expense, 4) if rw_expense else None,
        "Notes": f"LPG b-034321 wagon #{no}",
        "CreatedAtUtc": datetime.utcnow()
    })
    lpg_register_ids.append(rid)

# LPG Receipts (discharged at Akina terminal = terminal 2)
terminal_aqina = 2
ws_stocks = wb_lpg['stocks']
rows_stocks = list(ws_stocks.iter_rows(values_only=True))
# stocks has discharge data: wagon serial discharge info
# For simplicity, create receipts using loaded quantity
for i, r in enumerate(lpg_wagons):
    insert("LoadingReceipts", {
        "LoadingRegisterId": lpg_register_ids[i],
        "TerminalId": terminal_aqina,
        "ReceiptDate": r[1] if r[1] else datetime(2022, 12, 6),
        "ReceivedQuantityMt": round(float(r[4]), 4),
        "Notes": f"LPG b-034321 wagon {r[3]} arrived Akina",
        "CreatedAtUtc": datetime.utcnow()
    })

print(f"  Inserted {len(lpg_register_ids)} LPG wagons + receipts")
conn.commit()

# ──────────────────────────────────────────────
# 12. CUSTOMS DECLARATIONS — LPG b-034321 (28 wagons)
# ──────────────────────────────────────────────
print()
print("6. LPG Customs declarations (28 wagons)...")

ws_cust = wb_lpg['custom']
rows_cust = list(ws_cust.iter_rows(values_only=True))
lpg_customs = [r for r in rows_cust[4:] if r[0] is not None and isinstance(r[0], int)]

# Map wagon -> register id
lpg_wagon_to_rid = {}
for i, r in enumerate(lpg_wagons):
    lpg_wagon_to_rid[str(r[3])] = lpg_register_ids[i]

lpg_cust_count = 0
for r in lpg_customs:
    no = r[0]; date = r[1]; ref = r[2]; wagon = r[3]; weight = r[4]
    mahsooli_afn = r[5]; fawaid_afn = r[6]; mahsooli_usd = r[7]
    komision_tarifa = r[8]; norm = r[9]; khatt_ahan = r[10]
    elm = r[11]; gomrok_sarhadi = r[12]; komisyonkar = r[13]
    mosbat_buden = r[14]; motafarreqa = r[15]; fi_tan = r[16]; majmoo = r[17]

    wagon_str = str(wagon) if wagon else None
    rid = lpg_wagon_to_rid.get(wagon_str)
    if not rid:
        # Try matching by index
        if no <= len(lpg_register_ids):
            rid = lpg_register_ids[no - 1]
        else:
            continue

    total_afn = float(mahsooli_afn or 0) + float(fawaid_afn or 0)
    total_usd = float(majmoo or 0) if majmoo and isinstance(majmoo, (int, float)) else 0

    cd_id = insert("CustomsDeclarations", {
        "LoadingRegisterId": rid,
        "WagonOrTruckNumber": wagon_str,
        "DeclarationReference": str(int(ref)) if ref and isinstance(ref, (int, float)) else str(ref) if ref else None,
        "DeclarationDate": date if date else datetime(2022, 11, 25),
        "ConsignmentWeightMt": round(float(weight), 4) if weight and isinstance(weight, (int, float)) else None,
        "TotalAfn": round(total_afn, 4),
        "TotalUsd": round(total_usd, 4),
        "RatePerMtAfn": round(float(fi_tan), 4) if fi_tan and isinstance(fi_tan, (int, float)) else None,
        "Notes": f"LPG Akina customs, wagon {wagon}",
        "CreatedAtUtc": datetime.utcnow()
    })

    # Insert customs declaration items
    items = [
        (1, "محصولی (Customs Duty)", float(mahsooli_afn or 0), float(mahsooli_usd or 0)),
        (2, "فواید عامه (Public Welfare)", float(fawaid_afn or 0), 0),
        (3, "کمیشن تعرفه (Tariff Commission)", float(komision_tarifa or 0) * 22, float(komision_tarifa or 0)),
        (4, "نورم استندرد (Standard Norm)", float(norm or 0) * 22, float(norm or 0)),
        (5, "خط آهن (Railway)", float(khatt_ahan or 0) * 22, float(khatt_ahan or 0)),
        (6, "علم و خبر (Knowledge Fee)", float(elm or 0) * 22, float(elm or 0)),
        (7, "گمرک سرحدی (Border Customs)", float(gomrok_sarhadi or 0) * 22, float(gomrok_sarhadi or 0)),
        (8, "کمیشنکار (Agent Commission)", float(komisyonkar or 0) * 22, float(komisyonkar or 0)),
        (9, "متفرقه (Miscellaneous)", float(motafarreqa or 0) * 22, float(motafarreqa or 0)),
    ]
    for comp_type, label, amt_afn, amt_usd in items:
        if amt_afn > 0 or amt_usd > 0:
            insert("CustomsDeclarationItems", {
                "CustomsDeclarationId": cd_id,
                "ComponentType": comp_type,
                "CustomLabel": label,
                "AmountAfn": round(amt_afn, 4),
                "AmountUsd": round(amt_usd, 4),
                "CreatedAtUtc": datetime.utcnow()
            })
    lpg_cust_count += 1

print(f"  Inserted {lpg_cust_count} LPG customs declarations")
conn.commit()

# ──────────────────────────────────────────────
# 13. LPG SALES (from Sales in Akina sheet)
# ──────────────────────────────────────────────
print()
print("7. LPG Sales in Akina...")

ws_lpg_sales = wb_lpg['Sales in Akina']
rows_lpg_sales = list(ws_lpg_sales.iter_rows(values_only=True))
lpg_sales = [r for r in rows_lpg_sales[4:] if r[0] is not None and isinstance(r[0], int)]

lpg_sale_count = 0
for r in lpg_sales:
    no = r[0]; date = r[1]; detail = r[2]; source = r[3]; serial = r[4]; driver = r[5]
    truck = r[6]; weight = r[7]; price = r[8]; total = r[9]

    # Parse date
    if isinstance(date, datetime):
        sale_date = date
    elif isinstance(date, str):
        try:
            sale_date = datetime.strptime(date.strip(), '%Y-%m-%d')
        except:
            sale_date = datetime(2022, 12, 6)
    else:
        sale_date = datetime(2022, 12, 6)

    insert("SalesTransactions", {
        "CompanyId": co_sedegi,
        "ContractId": c_lpg,
        "CustomerId": cust_fawad,
        "ProductId": prod_lpg,
        "DestinationLocationId": loc_akina,
        "InvoiceNumber": f"LPG-AKN-{no:04d}",
        "SaleDate": sale_date,
        "QuantityMt": round(float(weight), 4) if isinstance(weight, (int, float)) else 0,
        "UnitPriceUsd": round(float(price), 4) if isinstance(price, (int, float)) else 0,
        "TotalUsd": round(float(total), 4) if isinstance(total, (int, float)) else 0,
        "Currency": "USD",
        "TotalInCurrency": round(float(total), 4) if isinstance(total, (int, float)) else 0,
        "UnitPriceInCurrency": round(float(price), 4) if isinstance(price, (int, float)) else 0,
        "TicketSerialNumber": str(int(serial)) if serial and isinstance(serial, (int, float)) else None,
        "StockSourceType": 0,
        "SaleStage": 1,
        "IsCancelled": False,
        "Notes": f"{detail} - Truck {truck}",
        "CreatedAtUtc": datetime.utcnow()
    })
    lpg_sale_count += 1

print(f"  Inserted {lpg_sale_count} LPG sales")
conn.commit()

# ──────────────────────────────────────────────
# 14. VOLGA VESSEL — Loading Register
# ──────────────────────────────────────────────
print()
print("8. VOLGA vessel loading register...")

wb_volga = openpyxl.load_workbook('attached_assets/VOLGA_1777772526492.xlsx', data_only=True)

# Create 2 loading registers for VOLGA (one per purchase)
rid_volga_bnk = insert("LoadingRegisters", {
    "ContractId": c_volga_bnk,
    "ProductId": prod_petrol,
    "OriginLocationId": loc_okarem,
    "VesselId": vessel_volga,
    "LoadingDate": datetime(2026, 2, 1),
    "LoadedQuantityMt": 2350.0,
    "LoadingPriceUsd": 600.73,
    "BillOfLadingNumber": "VOLGA-BNK-2026",
    "TransportType": 2,  # Vessel
    "ConsigneeName": "PETROGAZ TRADING",
    "DestinationName": "Herat, Afghanistan",
    "Notes": "Vessel VOLGA: BNK-SOLVEX portion, 2350 MT AI-92-K5 from Okarem",
    "CreatedAtUtc": datetime.utcnow()
})

rid_volga_ptg = insert("LoadingRegisters", {
    "ContractId": c_volga_ptg,
    "ProductId": prod_petrol,
    "OriginLocationId": loc_okarem,
    "VesselId": vessel_volga,
    "LoadingDate": datetime(2026, 2, 1),
    "LoadedQuantityMt": 1556.531,
    "LoadingPriceUsd": 570.0,
    "BillOfLadingNumber": "VOLGA-PTG-2026",
    "TransportType": 2,
    "ConsigneeName": "PETROGAZ TRADING",
    "DestinationName": "Herat, Afghanistan",
    "Notes": "Vessel VOLGA: PETROGAS/SOLVEX portion, 1556.531 MT AI-92-K5 from Okarem",
    "CreatedAtUtc": datetime.utcnow()
})

# Loading Receipts for VOLGA (arrived at Ilinka/Okarem terminal)
insert("LoadingReceipts", {
    "LoadingRegisterId": rid_volga_bnk,
    "TerminalId": terminal_ilinka,
    "ReceiptDate": datetime(2026, 2, 9),
    "ReceivedQuantityMt": 2350.0,
    "ReferenceDocument": "VOLGA Loading Report - BNK portion",
    "Notes": "Vessel Volga Bill of Lading 3906.531 MT total. BNK portion: 2350 MT",
    "CreatedAtUtc": datetime.utcnow()
})
insert("LoadingReceipts", {
    "LoadingRegisterId": rid_volga_ptg,
    "TerminalId": terminal_ilinka,
    "ReceiptDate": datetime(2026, 2, 9),
    "ReceivedQuantityMt": 1556.531,
    "ReferenceDocument": "VOLGA Loading Report - PTG/SOLVEX portion",
    "Notes": "Vessel Volga Bill of Lading 3906.531 MT total. Outturn 3888.88 MT",
    "CreatedAtUtc": datetime.utcnow()
})

print(f"  VOLGA loading registers: BNK={rid_volga_bnk}, PTG={rid_volga_ptg}")
conn.commit()

# ──────────────────────────────────────────────
# 15. VOLGA TRUCK DISPATCHES (126 trucks)
# ──────────────────────────────────────────────
print()
print("9. VOLGA Truck dispatches (126)...")

ws_trucks = wb_volga['کرایه موتر ها']
rows_trucks = list(ws_trucks.iter_rows(values_only=True))
truck_rows = [r for r in rows_trucks[2:] if r[0] is not None and isinstance(r[0], int)]

# Create all unique trucks first
truck_plate_to_id = {}
unique_plates = list(set(r[3] for r in truck_rows if r[3]))
for plate in unique_plates:
    plate_str = str(plate)[:50]
    cur.execute('SELECT "Id" FROM "Trucks" WHERE "PlateNumber" = %s LIMIT 1', (plate_str,))
    row = cur.fetchone()
    if row:
        truck_plate_to_id[plate_str] = row[0]
    else:
        tid = insert("Trucks", {
            "PlateNumber": plate_str,
            "IsActive": True,
            "CreatedAtUtc": datetime.utcnow()
        })
        truck_plate_to_id[plate_str] = tid

print(f"  Created {len(truck_plate_to_id)} trucks")

# Build VOLGA customs lookup by truck plate for linking later
ws_customs_volga = wb_volga['مصارف محصولی']
rows_cust_volga = list(ws_customs_volga.iter_rows(values_only=True))
volga_customs_by_plate = {}
for r in rows_cust_volga[2:]:
    if r[0] is not None and isinstance(r[0], int) and r[3]:
        plate = str(r[3])
        volga_customs_by_plate[plate] = {
            'date': r[1],
            'serial': str(int(r[2])) if r[2] and isinstance(r[2], (int, float)) else str(r[2]) if r[2] else None,
            'weight': r[4],
            'mahsooli': r[6],
            'fawaid': r[7],
            'norm': r[8],
            'haqulkhedma': r[9],
            'yozbulagh': r[10],
            'komisyon': r[11],
            'total_afn': r[12],
            'total_usd': r[13],
        }

# Determine which dispatches are to Herat vs Akina from sales data
ws_sales_h = wb_volga['فروشات هرات']
rows_sales_h = list(ws_sales_h.iter_rows(values_only=True))
sales_h = [r for r in rows_sales_h[3:] if r[0] is not None and isinstance(r[0], int)]
herat_plates = set(str(r[5]) for r in sales_h if r[5])

ws_sales_a = wb_volga['فروشات آقینه']
rows_sales_a = list(ws_sales_a.iter_rows(values_only=True))
sales_a = [r for r in rows_sales_a[3:] if r[0] is not None and isinstance(r[0], int)]
aqina_sale_trucks = set(str(r[5]) for r in sales_a if r[5])

# Dummy driver
cur.execute('SELECT "Id" FROM "Drivers" LIMIT 1')
driver_row = cur.fetchone()
if driver_row:
    dummy_driver = driver_row[0]
else:
    dummy_driver = insert("Drivers", {
        "FullName": "Unknown Driver",
        "IsActive": True,
        "CreatedAtUtc": datetime.utcnow()
    })

dispatch_ids_by_plate = {}
dispatch_count = 0

for r in truck_rows:
    no = r[0]; date = r[1]; serial = r[2]; plate = r[3]; loaded_mt = r[4]
    dest_str = r[5]; freight_rate = r[6]; freight_total = r[7]
    discharged_mt = r[8]; shortage = r[9]; tolerance = r[10]
    chargeable_shortage = r[11]; shortage_rate = r[12]; shortage_deduction = r[13]
    payable = r[15]

    plate_str = str(plate)[:50]
    truck_id = truck_plate_to_id.get(plate_str)
    if not truck_id:
        continue

    dest_loc = loc_herat if 'Herat' in str(dest_str or '') else loc_akina
    payable_usd = float(payable) if payable and isinstance(payable, (int, float)) else (float(freight_total) if freight_total and isinstance(freight_total, (int, float)) else None)

    did = insert("TruckDispatches", {
        "ContractId": c_volga_bnk,
        "ProductId": prod_petrol,
        "TruckId": truck_id,
        "DestinationLocationId": dest_loc,
        "DispatchDate": date if isinstance(date, datetime) else datetime(2026, 2, 3),
        "Status": 2,  # Delivered
        "LoadedQuantityMt": round(float(loaded_mt), 4) if isinstance(loaded_mt, (int, float)) else 0,
        "DischargedQuantityMt": round(float(discharged_mt), 4) if discharged_mt and isinstance(discharged_mt, (int, float)) else None,
        "ShortageMt": round(float(shortage), 4) if shortage and isinstance(shortage, (int, float)) else None,
        "ToleranceMt": round(float(tolerance), 4) if tolerance and isinstance(tolerance, (int, float)) else None,
        "ChargeableShortageMt": round(float(chargeable_shortage), 4) if chargeable_shortage and isinstance(chargeable_shortage, (int, float)) else None,
        "ShortageRateUsd": round(float(shortage_rate), 4) if shortage_rate and isinstance(shortage_rate, (int, float)) else None,
        "FreightCostUsd": round(float(freight_rate), 4) if freight_rate and isinstance(freight_rate, (int, float)) else None,
        "PayableUsd": round(payable_usd, 4) if payable_usd else None,
        "FreightPayableUsd": round(payable_usd, 4) if payable_usd else None,
        "TicketSerialNumber": str(int(serial)) if serial and isinstance(serial, (int, float)) else str(serial) if serial else None,
        "Notes": f"VOLGA truck #{no}, {dest_str}",
        "CreatedAtUtc": datetime.utcnow()
    })
    dispatch_ids_by_plate[plate_str] = did
    dispatch_count += 1

print(f"  Inserted {dispatch_count} truck dispatches")
conn.commit()

# ──────────────────────────────────────────────
# 16. VOLGA CUSTOMS (126 trucks)
# ──────────────────────────────────────────────
print()
print("10. VOLGA Customs declarations (126 trucks)...")

volga_cust_count = 0
for plate, cdata in volga_customs_by_plate.items():
    # Get the VOLGA loading register (use BNK as default)
    rid = rid_volga_bnk

    total_afn = float(cdata['total_afn']) if cdata['total_afn'] and isinstance(cdata['total_afn'], (int, float)) else 0
    total_usd = float(cdata['total_usd']) if cdata['total_usd'] and isinstance(cdata['total_usd'], (int, float)) else 0
    weight = float(cdata['weight']) if cdata['weight'] and isinstance(cdata['weight'], (int, float)) else None

    cd_id = insert("CustomsDeclarations", {
        "LoadingRegisterId": rid,
        "WagonOrTruckNumber": plate,
        "DeclarationReference": cdata['serial'],
        "DeclarationDate": cdata['date'] if isinstance(cdata['date'], datetime) else datetime(2026, 2, 3),
        "ConsignmentWeightMt": round(weight, 4) if weight else None,
        "TotalAfn": round(total_afn, 4),
        "TotalUsd": round(total_usd, 4),
        "RatePerMtUsd": round(total_usd / weight, 4) if weight and weight > 0 and total_usd > 0 else None,
        "Notes": f"VOLGA customs - Truck {plate}",
        "CreatedAtUtc": datetime.utcnow()
    })

    # Insert breakdown items
    mahsooli = float(cdata['mahsooli'] or 0)
    fawaid = float(cdata['fawaid'] or 0)
    norm = float(cdata['norm'] or 0)
    haqulkhedma = float(cdata['haqulkhedma'] or 0)
    yozbulagh = float(cdata['yozbulagh'] or 0)
    komisyon = float(cdata['komisyon'] or 0)

    for comp_type, label, amt_afn in [
        (1, "مصارف محصولی (Customs Duty)", mahsooli),
        (2, "مصارف فواید عامه (Public Welfare)", fawaid),
        (3, "نورم استندرد (Standard Norm)", norm),
        (4, "حق الخدمه مواد نفت (Oil Service Fee)", haqulkhedma),
        (5, "یوزبلاغ (Yozbulagh)", yozbulagh),
        (6, "کمیشن بارچلانی (Loading Commission)", komisyon),
    ]:
        if amt_afn > 0:
            insert("CustomsDeclarationItems", {
                "CustomsDeclarationId": cd_id,
                "ComponentType": comp_type,
                "CustomLabel": label,
                "AmountAfn": round(amt_afn, 4),
                "AmountUsd": 0,
                "CreatedAtUtc": datetime.utcnow()
            })
    volga_cust_count += 1

print(f"  Inserted {volga_cust_count} VOLGA customs declarations")
conn.commit()

# ──────────────────────────────────────────────
# 17. VOLGA SALES — Herat (138 records)
# ──────────────────────────────────────────────
print()
print("11. VOLGA Sales - Herat (138)...")

sale_herat_count = 0
for r in sales_h:
    no = r[0]; date_raw = r[1]; detail = r[2]; serial = r[3]; driver = r[4]
    plate = r[5]; weight = r[6]; price = r[7]; total = r[8]; balance = r[9]

    # Parse Afghan date (e.g. '22/11/1404')
    if isinstance(date_raw, datetime):
        sale_date = date_raw
    elif isinstance(date_raw, str):
        # Convert Shamsi date approximately (1404 = 2026)
        try:
            parts = date_raw.strip().split('/')
            day, month, year_sh = int(parts[0]), int(parts[1]), int(parts[2])
            # approximate conversion: 1404 ~ 2026
            year_g = year_sh - 1404 + 2026
            # Shamsi month to Gregorian (approximate)
            sale_date = datetime(year_g, max(1, min(12, month)), max(1, min(28, day)))
        except:
            sale_date = datetime(2026, 2, 10)
    else:
        sale_date = datetime(2026, 2, 10)

    insert("SalesTransactions", {
        "CompanyId": co_sedegi,
        "ContractId": c_volga_bnk,
        "CustomerId": cust_aziz,
        "ProductId": prod_petrol,
        "DestinationLocationId": loc_herat,
        "InvoiceNumber": f"VOLGA-HRT-{no:04d}",
        "SaleDate": sale_date,
        "QuantityMt": round(float(weight), 4) if isinstance(weight, (int, float)) else 0,
        "UnitPriceUsd": round(float(price), 4) if isinstance(price, (int, float)) else 0,
        "TotalUsd": round(float(total), 4) if isinstance(total, (int, float)) else 0,
        "Currency": "USD",
        "TotalInCurrency": round(float(total), 4) if isinstance(total, (int, float)) else 0,
        "UnitPriceInCurrency": round(float(price), 4) if isinstance(price, (int, float)) else 0,
        "StockSourceType": 1,  # from vessel
        "SaleStage": 1,
        "IsCancelled": False,
        "Notes": f"{detail} - Truck {plate}",
        "CreatedAtUtc": datetime.utcnow()
    })
    sale_herat_count += 1

print(f"  Inserted {sale_herat_count} Herat sales")
conn.commit()

# ──────────────────────────────────────────────
# 18. VOLGA SALES — Akina (4 records)
# ──────────────────────────────────────────────
print()
print("12. VOLGA Sales - Akina (4)...")

sale_aqina_count = 0
for r in sales_a:
    no = r[0]; date_raw = r[1]; detail = r[2]; serial = r[3]; driver = r[4]
    truck_no = r[5]; weight = r[6]; price = r[7]; total = r[8]

    if isinstance(date_raw, datetime):
        sale_date = date_raw
    elif isinstance(date_raw, str):
        try:
            parts = date_raw.strip().split('/')
            day, month, year_sh = int(parts[0]), int(parts[1]), int(parts[2])
            year_g = year_sh - 1404 + 2026
            sale_date = datetime(year_g, max(1, min(12, month)), max(1, min(28, day)))
        except:
            sale_date = datetime(2026, 2, 1)
    else:
        sale_date = datetime(2026, 2, 1)

    insert("SalesTransactions", {
        "CompanyId": co_sedegi,
        "ContractId": c_volga_bnk,
        "CustomerId": cust_sadeq,
        "ProductId": prod_petrol,
        "DestinationLocationId": loc_akina,
        "InvoiceNumber": f"VOLGA-AKN-{no:04d}",
        "SaleDate": sale_date,
        "QuantityMt": round(float(weight), 4) if isinstance(weight, (int, float)) else 0,
        "UnitPriceUsd": round(float(price), 4) if isinstance(price, (int, float)) else 0,
        "TotalUsd": round(float(total), 4) if isinstance(total, (int, float)) else 0,
        "Currency": "USD",
        "TotalInCurrency": round(float(total), 4) if isinstance(total, (int, float)) else 0,
        "UnitPriceInCurrency": round(float(price), 4) if isinstance(price, (int, float)) else 0,
        "TicketSerialNumber": str(int(serial)) if serial and isinstance(serial, (int, float)) else str(serial) if serial else None,
        "StockSourceType": 2,  # from tank
        "SaleStage": 1,
        "IsCancelled": False,
        "Notes": f"{detail}",
        "CreatedAtUtc": datetime.utcnow()
    })
    sale_aqina_count += 1

print(f"  Inserted {sale_aqina_count} Akina sales")
conn.commit()

# ──────────────────────────────────────────────
# SUMMARY
# ──────────────────────────────────────────────
print()
print("=" * 50)
print("DATA ENTRY COMPLETE")
print("=" * 50)

cur.execute('SELECT COUNT(*) FROM "LoadingRegisters"')
print(f"LoadingRegisters total: {cur.fetchone()[0]}")
cur.execute('SELECT COUNT(*) FROM "LoadingReceipts"')
print(f"LoadingReceipts total: {cur.fetchone()[0]}")
cur.execute('SELECT COUNT(*) FROM "TruckDispatches"')
print(f"TruckDispatches total: {cur.fetchone()[0]}")
cur.execute('SELECT COUNT(*) FROM "SalesTransactions"')
print(f"SalesTransactions total: {cur.fetchone()[0]}")
cur.execute('SELECT COUNT(*) FROM "CustomsDeclarations"')
print(f"CustomsDeclarations total: {cur.fetchone()[0]}")
cur.execute('SELECT COUNT(*) FROM "CustomsDeclarationItems"')
print(f"CustomsDeclarationItems total: {cur.fetchone()[0]}")
cur.execute('SELECT COUNT(*) FROM "Contracts"')
print(f"Contracts total: {cur.fetchone()[0]}")
cur.execute('SELECT COUNT(*) FROM "Trucks"')
print(f"Trucks total: {cur.fetchone()[0]}")
cur.execute('SELECT COUNT(*) FROM "Customers"')
print(f"Customers total: {cur.fetchone()[0]}")

conn.close()
print()
print("Done!")
