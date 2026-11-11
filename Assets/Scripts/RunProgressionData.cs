using NUnit.Framework;
using System;
using UnityEngine;

public enum PerkCategory
{
    TurretSpecific, // Afecta a una sola torreta aleatoria (indicada desde que aparece el drop)
    WagonGlobal     // Afecta a los nuevos vagones y/o a todas las nuevas torretas
}

public enum TurretPerkStat
{
    AttackSpeed_Plus15Pct, // +15% Velocidad de ataque
    AttackRange_Plus3m,    // +3m Rango de alcance
    BulletDamage_Plus15,   // +15 Daño de proyectiles (Torretas 1 a 4)
    WaterKnockback_Plus    // +Fuerza de empuje para Torreta 5 (Manguera de Agua)
}

public enum WagonPerkStat
{
    MaxHealthAndHeal,          // +50 Vida máxima y curación inmediata
    AllTurretsAttackSpeed_10,  // +10% Velocidad de ataque a todas las torretas nuevas
    AllTurretsRange_2m,        // +2m Rango de alcance a todas las torretas nuevas
    AllTurretsDamage_10Pct     // +10% Daño de proyectiles a todas las torretas nuevas
}

[Serializable]
public class RolledPerkInfo
{
    public PerkCategory category;
    public EnumCardType targetTurret;
    public TurretPerkStat turretStat;
    public WagonPerkStat wagonStat;
    public string displayTitle;
    public string displayPopupShort;
}

[Serializable]
public class SavedRunState
{
    public bool hasActiveSave = false;
    public int currentZoneLevel = 1;
    public string lastCollectedPerkDescription = "Ninguno todavía";

    // Bonos por tipo de torreta (índices 0 a 4 para Turret1..Turret5)
    public float[] turretAtkSpeedBonusPct = new float[5]; // Ej: 0.15 = +15%
    public float[] turretRangeBonusMeters = new float[5]; // Ej: 3.0 = +3m
    public float[] turretFlatDamageBonus = new float[5];  // Ej: 15 = +15 daño
    public float waterTurretKnockbackBonus = 0f;          // Bono de empuje para Turret5_WaterHose

    // Bonos de Vagón / Globales para nuevas torretas y nuevos vagones
    public float wagonExtraMaxHealth = 0f;
    public float globalTurretAtkSpeedPct = 0f; // +0.10 por perk
    public float globalTurretRangeMeters = 0f; // +2.0m por perk
    public float globalTurretDamagePct = 0f;   // +0.10 por perk
}

public static class RunProgressionData
{
    private const string SAVE_KEY = "TREN2112_SAVED_RUN_V1";
    private static SavedRunState currentState;

    public static SavedRunState State
    {
        get
        {
            if (currentState == null)
            {
                LoadFromDisk();
            }
            return currentState;
        }
    }

    public static bool HasSavedRun()
    {
        if (!PlayerPrefs.HasKey(SAVE_KEY)) return false;
        LoadFromDisk();
        return currentState != null && currentState.hasActiveSave;
    }

    public static void StartNewRun()
    {
        currentState = new SavedRunState
        {
            hasActiveSave = false,
            currentZoneLevel = 1,
            lastCollectedPerkDescription = "Partida nueva (Sin perks aún)"
        };
        SaveToDisk();
    }

    public static void SaveCurrentRunForContinue()
    {
        State.hasActiveSave = true;
        SaveToDisk();
        Debug.Log($"[Guardado] Partida guardada en Zona/Nivel {State.currentZoneLevel}.");
    }

    public static void ClearSavedRun()
    {
        PlayerPrefs.DeleteKey(SAVE_KEY);
        PlayerPrefs.Save();
        currentState = new SavedRunState();
    }

    public static void SaveToDisk()
    {
        if (currentState == null) currentState = new SavedRunState();
        string json = JsonUtility.ToJson(currentState);
        PlayerPrefs.SetString(SAVE_KEY, json);
        PlayerPrefs.Save();
    }

    public static void LoadFromDisk()
    {
        if (PlayerPrefs.HasKey(SAVE_KEY))
        {
            string json = PlayerPrefs.GetString(SAVE_KEY);
            currentState = JsonUtility.FromJson<SavedRunState>(json);
        }
        if (currentState == null)
        {
            currentState = new SavedRunState();
        }
    }

    public static int GetTurretIndex(EnumCardType type)
    {
        switch (type)
        {
            case EnumCardType.Turret1_Area: return 0;
            case EnumCardType.Turret2_Rapid: return 1;
            case EnumCardType.Turret3_Heavy: return 2;
            case EnumCardType.Turret4_Lanzallamas: return 3;
            case EnumCardType.Turret5_WaterHose: return 4;
            default: return 0;
        }
    }

    public static string GetFriendlyTurretName(EnumCardType type)
    {
        switch (type)
        {
            case EnumCardType.Turret1_Area: return "Torreta 1 (Área)";
            case EnumCardType.Turret2_Rapid: return "Torreta 2 (Rápida)";
            case EnumCardType.Turret3_Heavy: return "Torreta 3 (Pesada)";
            case EnumCardType.Turret4_Lanzallamas: return "Torreta 4 (Lanzallamas)";
            case EnumCardType.Turret5_WaterHose: return "Torreta 5 (Agua / Empuje)";
            default: return type.ToString();
        }
    }

    // Genera el Perk aleatorio desde el momento en que aparece el Tren Enemigo para mostrarlo en el Popup del Drop
    public static RolledPerkInfo GenerateRandomPerk()
    {
        RolledPerkInfo info = new RolledPerkInfo();

        // 60% probabilidad de Perk para una Torreta específica, 40% Perk de Vagón
        if (UnityEngine.Random.value < 0.60f)
        {
            info.category = PerkCategory.TurretSpecific;
            EnumCardType[] turrets = new EnumCardType[]
            {
                EnumCardType.Turret1_Area,
                EnumCardType.Turret2_Rapid,
                EnumCardType.Turret3_Heavy,
                EnumCardType.Turret4_Lanzallamas,
                EnumCardType.Turret5_WaterHose
            };
            info.targetTurret = turrets[UnityEngine.Random.Range(0, turrets.Length)];
            string tName = GetFriendlyTurretName(info.targetTurret);

            if (info.targetTurret == EnumCardType.Turret5_WaterHose)
            {
                // La torreta de agua no hace daño sino que empuja enemigos: puede tocarle +Empuje, +Vel. Ataque o +Rango
                int roll = UnityEngine.Random.Range(0, 3);
                if (roll == 0)
                {
                    info.turretStat = TurretPerkStat.WaterKnockback_Plus;
                    info.displayPopupShort = $"{tName} + 40 % Fuerza de Empuje(+2m)";
                    info.displayTitle = $"Perk de {tName}: +40% Fuerza de Empuje (+2.0m)";
                }
                else if (roll == 1)
                {
                    info.turretStat = TurretPerkStat.AttackSpeed_Plus15Pct;
                    info.displayPopupShort = $"{tName} + 15 % Velocidad de Ataque";
                    info.displayTitle = $"Perk de {tName}: +15% Velocidad de Ataque";
                }
                else
                {
                    info.turretStat = TurretPerkStat.AttackRange_Plus3m;
                    info.displayPopupShort = $"{tName} + 3m Rango de Alcance";
                    info.displayTitle = $"Perk de {tName}: +3m Rango de Alcance";
                }
            }
            else
            {
                int roll = UnityEngine.Random.Range(0, 3);
                if (roll == 0)
                {
                    info.turretStat = TurretPerkStat.AttackSpeed_Plus15Pct;
                    info.displayPopupShort = $"{tName} + 15 % Velocidad de Ataque";
                    info.displayTitle = $"Perk de {tName}: +15% Velocidad de Ataque";
                }
                else if (roll == 1)
                {
                    info.turretStat = TurretPerkStat.AttackRange_Plus3m;
                    info.displayPopupShort = $"{tName} + 3m Rango de Alcance";
                    info.displayTitle = $"Perk de {tName}: +3m Rango de Alcance";
                }
                else
                {
                    info.turretStat = TurretPerkStat.BulletDamage_Plus15;
                    info.displayPopupShort = $"{tName} + 15 Daño de Proyectil";
                    info.displayTitle = $"Perk de {tName}: +15 Daño de Proyectiles";
                }
            }
        }
        else
        {
            info.category = PerkCategory.WagonGlobal;
            int roll = UnityEngine.Random.Range(0, 4);
            if (roll == 0)
            {
                info.wagonStat = WagonPerkStat.MaxHealthAndHeal;
                info.displayPopupShort = "PERK DE VAGÓN + 50 Vida Máx y Curación";
                info.displayTitle = "Perk de Vagón: +50 Vida Máxima y Curación Inmediata";
            }
            else if (roll == 1)
            {
                info.wagonStat = WagonPerkStat.AllTurretsAttackSpeed_10;
                info.displayPopupShort = "PERK DE VAGÓN + 10 % Vel.Ataque(Todas las Torretas)";
                info.displayTitle = "Perk de Vagón: +10% Velocidad de Ataque Global";
            }
            else if (roll == 2)
            {
                info.wagonStat = WagonPerkStat.AllTurretsRange_2m;
                info.displayPopupShort = "PERK DE VAGÓN + 2m Rango(Todas las Torretas)";
                info.displayTitle = "Perk de Vagón: +2m Rango de Alcance Global";
            }
            else
            {
                info.wagonStat = WagonPerkStat.AllTurretsDamage_10Pct;
                info.displayPopupShort = "PERK DE VAGÓN + 10 % Daño(Todas las Torretas)";
                info.displayTitle = "Perk de Vagón: +10% Daño de Proyectiles Global";
            }
        }

        return info;
    }

    public static void ApplyCollectedPerk(RolledPerkInfo perk)
    {
        if (perk == null) return;
        SavedRunState s = State;

        if (perk.category == PerkCategory.TurretSpecific)
        {
            int idx = GetTurretIndex(perk.targetTurret);
            switch (perk.turretStat)
            {
                case TurretPerkStat.AttackSpeed_Plus15Pct:
                    s.turretAtkSpeedBonusPct[idx] += 0.15f;
                    break;
                case TurretPerkStat.AttackRange_Plus3m:
                    s.turretRangeBonusMeters[idx] += 3f;
                    break;
                case TurretPerkStat.BulletDamage_Plus15:
                    s.turretFlatDamageBonus[idx] += 15f;
                    break;
                case TurretPerkStat.WaterKnockback_Plus:
                    s.waterTurretKnockbackBonus += 2.0f;
                    break;
            }
        }
        else
        {
            switch (perk.wagonStat)
            {
                case WagonPerkStat.MaxHealthAndHeal:
                    s.wagonExtraMaxHealth += 50f;
                    // Curar vagones actuales si es curación inmediata
                    if (GameManager.Instance != null)
                    {
                        foreach (var w in GameManager.Instance.activeWagons)
                        {
                            if (w != null) w.HealWagon(100f);
                        }
                    }
                    break;
                case WagonPerkStat.AllTurretsAttackSpeed_10:
                    s.globalTurretAtkSpeedPct += 0.10f;
                    break;
                case WagonPerkStat.AllTurretsRange_2m:
                    s.globalTurretRangeMeters += 2f;
                    break;
                case WagonPerkStat.AllTurretsDamage_10Pct:
                    s.globalTurretDamagePct += 0.10f;
                    break;
            }
        }

        s.lastCollectedPerkDescription = perk.displayTitle;
        SaveCurrentRunForContinue();
        Debug.Log($"[Perk Aplicado y Guardado] {perk.displayTitle}");
    }

    // Consultas de bonos para aplicar ÚNICAMENTE a las nuevas torretas y nuevos vagones que se instancien
    public static float GetTotalAttackSpeedMultiplierForNewTurret(EnumCardType type)
    {
        int idx = GetTurretIndex(type);
        return 1f + State.turretAtkSpeedBonusPct[idx] + State.globalTurretAtkSpeedPct;
    }

    public static float GetTotalExtraRangeForNewTurret(EnumCardType type)
    {
        int idx = GetTurretIndex(type);
        return State.turretRangeBonusMeters[idx] + State.globalTurretRangeMeters;
    }

    public static float CalculateFinalDamageForNewTurret(EnumCardType type, float baseDamage)
    {
        int idx = GetTurretIndex(type);
        float withFlat = baseDamage + State.turretFlatDamageBonus[idx];
        return withFlat * (1f + State.globalTurretDamagePct);
    }

    public static float GetWaterKnockbackForce(float baseKnockback)
    {
        return baseKnockback + State.waterTurretKnockbackBonus;
    }

    public static float GetExtraMaxHealthForNewWagon()
    {
        return State.wagonExtraMaxHealth;
    }
}