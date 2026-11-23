---
name: kiloledger-sync-data
description: Change KiloLedger data formats, stable record IDs, ZIP backups, merge behavior, or Android/KPK/Google synchronization. Use whenever records, deletes, imports, or sync payloads are affected.
---

# KiloLedger data and synchronization

1. Read `README.md` data/synchronization sections and inspect the relevant code in all affected clients:
   - IDs and tombstones: each platform's `RecordIds.cs`
   - Android Wi-Fi sync: `KiloLedger.Android/ExpenseSyncService.cs`
   - Desktop merge: `KiloLedger.Desktop/Data/SyncMerge.cs`
   - KPK merge: `KiloLedger/KiloLedger/SyncMerge.cs`
2. Treat `.dat` layouts as a cross-platform contract. Preserve old rows and optional fields; validate new fields without silently dropping malformed data.
3. For deletable ID-backed records, keep entity-type mapping, tombstone creation, merge filtering, backup inclusion, and restore behavior consistent on every applicable client.
4. Keep ID prefixes and tombstone entity types consistent across platforms. Check `build/verify_sync_ids.py` and extend it for new mappings or deletion cases.
5. Protect user data: do not modify real data files, remove backups, or weaken safety-copy behavior. Prefer temporary fixtures and focused offline tests.
6. Run `python3 build/run_verifies.py`, `./build/build-kpk.sh Release`, and builds for other affected clients when available.
7. Update the data-format and synchronization documentation when the contract or supported clients change.
