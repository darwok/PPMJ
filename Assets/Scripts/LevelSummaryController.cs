using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelSummaryController : MonoBehaviour
{
    [Header("Nombres de Escenas")]
    [SerializeField] private string startMenuScene = "StartMenu";
    [SerializeField] private string gameScene = "GameScene";

    private GUIStyle panelStyle;
    private GUIStyle titleStyle;
    private GUIStyle subTitleStyle;
    private GUIStyle tableHeaderStyle;
    private GUIStyle tableCellStyle;
    private GUIStyle buttonStyle;

    // Valores base de referencia de las 5 torretas para mostrar Base + Bono Perk = Total
    private readonly string[] turretNames = new string[]
    {
        "Torreta 1 (Área / Esquirlas)",
        "Torreta 2 (Cadencia Rápida)",
        "Torreta 3 (Cañón Pesado)",
        "Torreta 4 (Lanzallamas)",
        "Torreta 5 (Agua / Empuje)"
    };
    private readonly float[] baseDamages = new float[] { 25f, 15f, 60f, 3f, 0f };
    private readonly float[] baseFireRates = new float[] { 1.2f, 0.4f, 1.8f, 0.08f, 0.25f };
    private readonly float[] baseRanges = new float[] { 14f, 18f, 20f, 12f, 15f };
    private readonly float baseWaterKnockback = 3.5f;

    private void InitStyles()
    {
        if (panelStyle != null) return;

        panelStyle = new GUIStyle(GUI.skin.box);
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = Color.white;

        subTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        subTitleStyle.normal.textColor = new Color(1f, 0.88f, 0.25f, 1f);

        tableHeaderStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold
        };
        tableHeaderStyle.normal.textColor = Color.cyan;

        tableCellStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            fontStyle = FontStyle.Normal
        };
        tableCellStyle.normal.textColor = Color.white;

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold
        };
        buttonStyle.normal.textColor = Color.white;
    }

    void OnGUI()
    {
        InitStyles();

        SavedRunState s = RunProgressionData.State;
        float w = Mathf.Min(1180f, Screen.width - 60f);
        float h = Mathf.Min(680f, Screen.height - 60f);
        float x = (Screen.width - w) * 0.5f;
        float y = (Screen.height - h) * 0.5f;

        GUI.Box(new Rect(x, y, w, h), "", panelStyle);

        GUI.Label(new Rect(x + 20f, y + 20f, w - 40f, 40f), $"RESUMEN DE ZONA COMPLETADA — NIVEL / ZONA ACTUAL: {s.currentZoneLevel}", titleStyle);
        GUI.Label(new Rect(x + 20f, y + 65f, w - 40f, 30f), $"Último Perk Obtenido del Tren Enemigo: {s.lastCollectedPerkDescription}", subTitleStyle);

        // Encabezados de tabla de Torretas
        float tableY = y + 115f;
        GUI.Label(new Rect(x + 35f, tableY, 260f, 28f), "Torreta / Unidad", tableHeaderStyle);
        GUI.Label(new Rect(x + 305f, tableY, 270f, 28f), "Daño / Empuje (Base + Perk = Total)", tableHeaderStyle);
        GUI.Label(new Rect(x + 590f, tableY, 280f, 28f), "Vel. Ataque (Base + Perk = Total)", tableHeaderStyle);
        GUI.Label(new Rect(x + 885f, tableY, 260f, 28f), "Rango (Base + Perk = Total)", tableHeaderStyle);

        for (int i = 0; i < 5; i++)
        {
            float rowY = tableY + 38f + (i * 46f);
            GUI.Label(new Rect(x + 35f, rowY, 260f, 30f), turretNames[i], tableCellStyle);

            // Columna Daño o Empuje (si es Torreta 5 de Agua)
            if (i == 4)
            {
                float kbBase = baseWaterKnockback;
                float kbExtra = s.waterTurretKnockbackBonus;
                float kbTotal = kbBase + kbExtra;
                GUI.Label(new Rect(x + 305f, rowY, 270f, 30f), $"Empuje: {kbBase:F1}m + {kbExtra:F1}m = {kbTotal:F1}m", tableCellStyle);
            }
            else
            {
                float dBase = baseDamages[i];
                float dFlat = s.turretFlatDamageBonus[i];
                float dGlobalPct = s.globalTurretDamagePct * 100f;
                float dTotal = (dBase + dFlat) * (1f + s.globalTurretDamagePct);
                GUI.Label(new Rect(x + 305f, rowY, 270f, 30f), $"{dBase:F0} (+{dFlat:F0} / +{dGlobalPct:F0}%) = {dTotal:F1}", tableCellStyle);
            }

            // Columna Velocidad de Ataque
            float spdPct = (s.turretAtkSpeedBonusPct[i] + s.globalTurretAtkSpeedPct) * 100f;
            float baseShotsPerSec = 1f / baseFireRates[i];
            float totalShotsPerSec = baseShotsPerSec * (1f + s.turretAtkSpeedBonusPct[i] + s.globalTurretAtkSpeedPct);
            GUI.Label(new Rect(x + 590f, rowY, 280f, 30f), $"{baseShotsPerSec:F1}/s (+{spdPct:F0}%) = {totalShotsPerSec:F1} disp/s", tableCellStyle);

            // Columna Rango
            float rBase = baseRanges[i];
            float rExtra = s.turretRangeBonusMeters[i] + s.globalTurretRangeMeters;
            float rTotal = rBase + rExtra;
            GUI.Label(new Rect(x + 885f, rowY, 260f, 30f), $"{rBase:F0}m + {rExtra:F0}m = {rTotal:F0}m", tableCellStyle);
        }

        // Fila resumen de Vagones Nuevos
        float wagonRowY = tableY + 38f + (5 * 46f) + 15f;
        float wBaseHp = 150f;
        float wExtraHp = s.wagonExtraMaxHealth;
        GUI.Label(new Rect(x + 35f, wagonRowY, 260f, 30f), "Vagones Nuevos (Estructura)", tableHeaderStyle);
        GUI.Label(new Rect(x + 305f, wagonRowY, 820f, 30f),
            $"Vida Máx: {wBaseHp:F0} + {wExtraHp:F0} = {wBaseHp + wExtraHp:F0} HP   |   Bonos Globales a Torretas: +{s.globalTurretAtkSpeedPct * 100f:F0}% Vel, +{s.globalTurretRangeMeters:F0}m Rango, +{s.globalTurretDamagePct * 100f:F0}% Daño",
            tableCellStyle);

        // Dos botones inferiores: Guardar y Salir al Menú Principal | Continuar al Siguiente Nivel
        float btnY = y + h - 85f;
        if (GUI.Button(new Rect(x + 80f, btnY, 440f, 60f), "💾 Guardar y Salir al Menú Principal", buttonStyle))
        {
            SaveAndReturnToMainMenu();
        }

        if (GUI.Button(new Rect(x + w - 520f, btnY, 440f, 60f), $"▶ Continuar al Siguiente Nivel (Zona {s.currentZoneLevel + 1})", buttonStyle))
        {
            ContinueToNextLevel();
        }
    }

    public void SaveAndReturnToMainMenu()
    {
        RunProgressionData.State.currentZoneLevel++;
        RunProgressionData.SaveCurrentRunForContinue();
        SceneManager.LoadScene(startMenuScene);
    }

    public void ContinueToNextLevel()
    {
        RunProgressionData.State.currentZoneLevel++;
        RunProgressionData.SaveCurrentRunForContinue();
        SceneManager.LoadScene(gameScene);
    }
}