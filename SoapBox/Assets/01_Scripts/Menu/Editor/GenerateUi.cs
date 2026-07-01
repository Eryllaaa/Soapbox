#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using Soapbox.Menu;

namespace Soapbox.EditorTools
{
    public static class SoapboxUIBuilderEditor
    {
        [MenuItem("Tools/Soapbox/1. Générer UI Menu (Main Menu)")]
        public static void BuildMainMenu()
        {
            // 1. Setup Canvas
            GameObject canvasGO = CreateCanvas("MainMenuCanvas");
            MainMenuUI menuUI = canvasGO.AddComponent<MainMenuUI>();

            // 2. Background
            CreateBackground(canvasGO.transform);

            // 3. Main Panel (Centré)
            GameObject mainPanel = CreatePanel(canvasGO.transform, "MainPanel", new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(400, 600));
            VerticalLayoutGroup vlg = mainPanel.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 20;
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;

            // Titre
            CreateText(mainPanel.transform, "Title", "SOAPBOX RACING", 48, FontStyle.Bold, 80);

            // 4. Steam Panel
            GameObject steamPanel = CreatePanel(mainPanel.transform, "SteamPanel", Vector2.zero, Vector2.one, new Vector2(400, 150));
            VerticalLayoutGroup steamVlg = steamPanel.AddComponent<VerticalLayoutGroup>();
            steamVlg.spacing = 10;
            steamVlg.childControlHeight = false;
            steamVlg.childForceExpandHeight = false;
            
            CreateText(steamPanel.transform, "SteamLabel", "-- STEAM --", 20, FontStyle.Italic, 30);
            Button hostSteamBtn = CreateButton(steamPanel.transform, "Btn_HostSteam", "Héberger (Steam)");
            Button joinSteamBtn = CreateButton(steamPanel.transform, "Btn_JoinSteam", "Rejoindre (Amis Steam)");

            // 5. LAN Panel
            GameObject lanPanel = CreatePanel(mainPanel.transform, "LanPanel", Vector2.zero, Vector2.one, new Vector2(400, 200));
            VerticalLayoutGroup lanVlg = lanPanel.AddComponent<VerticalLayoutGroup>();
            lanVlg.spacing = 10;
            lanVlg.childControlHeight = false;
            lanVlg.childForceExpandHeight = false;

            CreateText(lanPanel.transform, "LanLabel", "-- LAN --", 20, FontStyle.Italic, 30);
            InputField ipInput = CreateInputField(lanPanel.transform, "Input_IP", "127.0.0.1 (IP LAN)");
            Button hostLanBtn = CreateButton(lanPanel.transform, "Btn_HostLan", "Héberger (LAN)");
            Button joinLanBtn = CreateButton(lanPanel.transform, "Btn_JoinLan", "Rejoindre (LAN)");

            // 6. Quit
            Button quitBtn = CreateButton(mainPanel.transform, "Btn_Quit", "Quitter le jeu");

            // 7. Auto-Assignation des variables du script MainMenuUI via SerializedObject
            SerializedObject so = new SerializedObject(menuUI);
            so.FindProperty("steamPanel").objectReferenceValue = steamPanel;
            so.FindProperty("hostSteamButton").objectReferenceValue = hostSteamBtn;
            so.FindProperty("joinSteamButton").objectReferenceValue = joinSteamBtn;
            
            so.FindProperty("lanPanel").objectReferenceValue = lanPanel;
            so.FindProperty("hostLanButton").objectReferenceValue = hostLanBtn;
            so.FindProperty("joinLanButton").objectReferenceValue = joinLanBtn;
            so.FindProperty("ipInputField").objectReferenceValue = ipInput;
            
            so.FindProperty("quitButton").objectReferenceValue = quitBtn;
            so.ApplyModifiedProperties();

            // S'assurer qu'il y a un EventSystem
            EnsureEventSystem();

            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Main Menu");
            Selection.activeGameObject = canvasGO;
            Debug.Log("✅ Main Menu UI généré avec succès !");
        }

        [MenuItem("Tools/Soapbox/2. Générer UI Lobby (Attente)")]
        public static void BuildLobby()
        {
            // 1. Setup Canvas
            GameObject canvasGO = CreateCanvas("LobbyCanvas");
            LobbyUI lobbyUI = canvasGO.AddComponent<LobbyUI>();

            // 2. Background
            CreateBackground(canvasGO.transform);

            // 3. Main Panel (Centré)
            GameObject mainPanel = CreatePanel(canvasGO.transform, "MainPanel", new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(600, 500));
            VerticalLayoutGroup vlg = mainPanel.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 25;
            vlg.childControlHeight = false;
            vlg.childForceExpandHeight = false;

            // Titre & Statut
            CreateText(mainPanel.transform, "Title", "LOBBY D'ATTENTE", 40, FontStyle.Bold, 60);
            Text statusTxt = CreateText(mainPanel.transform, "Txt_Status", "Joueurs : 1/4\n(Personne)", 24, FontStyle.Normal, 150);
            statusTxt.alignment = TextAnchor.UpperCenter;

            // Boutons
            Button startRaceBtn = CreateButton(mainPanel.transform, "Btn_StartRace", "LANCER LA COURSE");
            Button inviteBtn = CreateButton(mainPanel.transform, "Btn_Invite", "Inviter un ami (Steam)");
            Button leaveBtn = CreateButton(mainPanel.transform, "Btn_Leave", "Quitter le Lobby");

            // 4. Auto-Assignation
            SerializedObject so = new SerializedObject(lobbyUI);
            so.FindProperty("startRaceButton").objectReferenceValue = startRaceBtn;
            so.FindProperty("inviteFriendButton").objectReferenceValue = inviteBtn;
            so.FindProperty("leaveButton").objectReferenceValue = leaveBtn;
            so.FindProperty("statusText").objectReferenceValue = statusTxt;
            so.ApplyModifiedProperties();

            EnsureEventSystem();

            Undo.RegisterCreatedObjectUndo(canvasGO, "Create Lobby UI");
            Selection.activeGameObject = canvasGO;
            Debug.Log("✅ Lobby UI généré avec succès !");
        }

        // ==========================================
        // OUTILS DE CREATION RAPIDE (KISS & DRY)
        // ==========================================

        private static GameObject CreateCanvas(string name)
        {
            GameObject go = new GameObject(name);
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return go;
        }

        private static void CreateBackground(Transform parent)
        {
            GameObject bg = new GameObject("Background");
            bg.transform.SetParent(parent, false);
            Image img = bg.AddComponent<Image>();
            img.color = new Color(0.1f, 0.12f, 0.15f, 1f); // Gris foncé
            
            RectTransform rt = bg.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 anchor, Vector2 anchoredPos, Vector2 size)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            RectTransform rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            return panel;
        }

        private static Button CreateButton(Transform parent, string name, string label)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.6f, 0.8f, 1f); // Bleu sympa
            Button btn = go.AddComponent<Button>();

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(350, 60);

            Text txt = CreateText(go.transform, "Text", label, 24, FontStyle.Bold, 60);
            txt.color = Color.white;
            
            return btn;
        }

        private static Text CreateText(Transform parent, string name, string content, int fontSize, FontStyle style, float height)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Text txt = go.AddComponent<Text>();
            txt.text = content;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = fontSize;
            txt.fontStyle = style;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(400, height);

            return txt;
        }

        private static InputField CreateInputField(Transform parent, string name, string placeholderText)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.9f, 0.9f, 0.9f, 1f);
            
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(350, 50);

            InputField inputField = go.AddComponent<InputField>();

            Text textComp = CreateText(go.transform, "Text", "", 20, FontStyle.Normal, 50);
            textComp.color = Color.black;
            textComp.alignment = TextAnchor.MiddleLeft;
            textComp.GetComponent<RectTransform>().sizeDelta = new Vector2(330, 50);

            Text placeholder = CreateText(go.transform, "Placeholder", placeholderText, 20, FontStyle.Italic, 50);
            placeholder.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            placeholder.alignment = TextAnchor.MiddleLeft;
            placeholder.GetComponent<RectTransform>().sizeDelta = new Vector2(330, 50);

            inputField.textComponent = textComp;
            inputField.placeholder = placeholder;

            return inputField;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();

                // On utilise le module du NOUVEAU Input System !
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
        }
    }
}
#endif