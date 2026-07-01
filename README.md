# Soapbox

Jeu multijoueur de **courses de caisses à savon** (soapbox racing) basé sur la physique, développé sur **Unity 6.5** avec **Mirror** (réseau) et **Fizzy Steamworks** (transport via Steam/Steamworks).

---

## Concept

Une caisse à savon, c'est un véhicule sans moteur, sans assistance, **propulsé uniquement par la gravité**. Le joueur contrôle le braquage et le freinage sur des pistes en pente, et compte sur la pente, l'inertie, le relief, et les collisions pour aller vite (ou survivre).

Le parti pris physique est délibéré :

- **Pas d'accélération**. La voiture prend de la vitesse quand ça descend, en perd quand ça monte. Point.
- **Le braquage et le freinage sont les seules commandes**.
- **L'inertie est ton ennemi et ton allié** : un mauvais virage à 30 km/h dans une pente, ça se paie cash.
- **Les collisions entre joueurs sont possibles** — sortir un adversaire de la trajectoire est une stratégie valide.

L'ambition multijoueur est de pouvoir faire des **courses de 4 à 8 joueurs** en LAN ou via Steam, en utilisant le transport Fizzy Steamworks de Mirror (pas de serveur dédié externe requis).

---

## Stack technique

| Domaine | Techno |
|---|---|
| Moteur | Unity 6.5 (`ProjectSettings/`) |
| Langage | C# (Mono / IL2CPP) |
| Réseau | Mirror (`Assets/Mirror/`) |
| Transport | Fizzy Steamworks (`Assets/Mirror/Transports/FizzySteamworks/`) |
| Steamworks | `com.rlabrecque.steamworks.net` |
| Inputs | Unity Input System (Package `com.unity.inputsystem` 1.19) |
| UI helpers | NaughtyAttributes (inspecteur) |
| Rendu | Universal Render Pipeline 17.5 |

---

## Architecture du projet

### Arborescence des scripts

```
Assets/01_Scripts/
├── Car/
│   ├── VehicleController.cs   ← Orchestrateur (réseau + physique)
│   ├── Wheel.cs               ← Physique de la roue (grip, frein, roulement)
│   └── Suspension.cs          ← Spring + raycast, déplace le Transform de la roue
├── Camera/
│   └── CameraRig.cs           ← Follow camera (multi-aware, suit le joueur local)
├── Network/
│   ├── SoapboxNetworkManager.cs  ← NetworkManager custom (spawn, Fizzy transport, host race trigger)
│   └── SoapboxSpawnPoint.cs      ← Marker de spawn, registry auto
├── Race/                        ← Système de course complet (event-based)
│   ├── EventManager.cs             ← Hub statique d'événements (subscribe/publish)
│   ├── RaceEventKeys.cs            ← Constantes des noms d'events + payload structs
│   ├── Checkpoint.cs               ← MonoBehaviour trigger pur, ignorant du réseau
│   ├── FinishLine.cs               ← Spécialisation de Checkpoint (sémantique)
│   ├── CheckpointManager.cs        ← Serveur: ledger des passages + classement live
│   ├── VehicleCheckpointTracker.cs ← NetworkBehaviour sur prefab véhicule (validation ordre)
│   ├── CountdownManager.cs         ← Compte à rebours 3-2-1-GO autoritaire serveur
│   ├── RaceManager.cs              ← State machine (Lobby→Countdown→Racing→Finished)
│   └── RaceHUD.cs                  ← UGUI local (décompte, position, classement final)
├── World/
│   └── SpeedPad.cs             ← Boost trigger (déjà présent)
└── Scriptable Scripts/
    └── Wheel Data.cs          ← ScriptableObject (placeholder, pas encore utilisé)
```

### Responsabilités — règle d'or

**`Wheel.cs` et `Suspension.cs` ne connaissent RIEN au réseau.** Ce sont des `MonoBehaviour` purs, identiques à la version single-player d'origine. Ils sont activés / désactivés depuis `VehicleController.OnStartClient()` selon que le client local possède ou non le véhicule.

**`VehicleController.cs` est le seul fichier qui contient la logique réseau.** C'est le point d'entrée pour toute évolution du modèle d'autorité, du transport, ou des inputs.

### Modèle d'autorité : Client Authority

C'est un choix conscient, documenté en en-tête de `VehicleController.cs` :

- Le **client propriétaire** (`isOwned == true`) lit l'input, simule la physique (Wheel + Suspension + Rigidbody) en local, exactement comme en solo.
- Le résultat (position / rotation du Rigidbody) est répliqué aux autres clients via un **`NetworkTransformReliable`** (sync direction `Client To Server`) posé sur le prefab véhicule.
- Les **clients distants** ne simulent rien : `OnStartClient` désactive Wheel et Suspension pour ce véhicule, et la position reçue par le NetworkTransform s'affiche seule.

**Pourquoi pas server authority ?**

- Pas d'enjeu anti-cheat critique dans le périmètre actuel (pas de score compétitif, pas de PvP avec dégâts).
- Server authority ajouterait de la duplication d'input + lag compensation sans bénéfice proportionné.
- Garder Wheel / Suspension 100 % ignorants du réseau = DRY, séparation claire des responsabilités, code testable en solo.

**Si un jour il faut passer en server authority** (mode classé, anti-cheat, etc.) : seul `VehicleController.cs` change. Remplacer la lecture d'input directe par un `[Command]`, faire tourner `FixedUpdate` côté serveur. Wheel et Suspension n'ont AUCUNE modification à subir puisqu'ils ne dépendent que d'un Rigidbody local, quel que soit qui le pilote.

### Mode solo

`VehicleController.IsOffline()` détecte l'absence de session réseau active (`!NetworkServer.active && !NetworkClient.active`) et **court-circuite toutes les gardes réseau** pour que les scènes de test sans NetworkManager continuent de fonctionner comme en single-player.

- `OnStartClient` ne désactive rien en solo.
- `FixedUpdate` simule même si `isOwned == false` en solo.
- `OnStartAuthority` / `OnStopAuthority` bindent / unbindent les inputs normalement (le helper `EnableLocalInput` / `DisableLocalInput` est mutualisé).

### Pipeline physique par FixedUpdate (côté autorité)

```
FixedUpdate()
  └─ if (!isOwned && !IsOffline()) return   ← garde réseau / solo
  ├─ ClampSpeed()             ← Rigidbody.linearVelocity ≤ _maxSpeed
  ├─ HandleSteering()         ← MoveTowards(_currentSteerAngle, _steerInput * _maxSteerAngle, ...)
  │                            puis application directe sur les Transform des steering wheels
  ├─ HandleBraking()          ← Appelle Wheel.Brake() / StopBraking() sur les brake wheels
  └─ HandleAirControl()       ← Si aucune roue grounded : AddTorque pitch (Z/S) et yaw (Q/D)
                               autour des axes LOCAUX du Rigidbody. Couplé à Wheel.IsGrounded.
                               Torques par défaut : 1500 Nm (à ajuster au feeling).
```

L'application de la rotation des wheels se fait **dans le même FixedUpdate** que le calcul de l'angle, pour que `Wheel.ApplySideFriction` (qui lit `transform.right` dans son propre FixedUpdate) reçoive la bonne valeur. C'est ce qui donne le feeling réactif du steering.

### Input System

- Binding par défaut (mapping ZQSD complet, voir `Assets/03_Assets/Inputs/InputActions.inputactions`) :
  - **Q / D** : steer au sol **ET** yaw en l'air (même touches, contexte discriminé par l'état grounded)
  - **Z / S** : pitch en l'air uniquement (pas de marche arrière, la caisse n'a pas de moteur)
  - **Espace** : frein
- Le mapping est full QWERTY : pas de gestion AZERTY côté code, les touches restent celles du composite. Si besoin AZERTY, modifier le `.inputactions` directement (les paths `Keyboard/q` etc. font foi).
- L'`InputActions` est instancié par le `VehicleController` lui-même, pas par un composant séparé. `OnEnable` (côté `OnStartAuthority`) bind les events, `OnDisable` les unbind. Pas de fuite.
- `OnDestroy` garantit un `Disable()` final sur l'asset d'input, peu importe le mode.
- Le double usage Q/D (steer sol / yaw air) est résolu côté code dans `VehicleController.HandleAirControl()` par une garde `IsAnyWheelGrounded()` : tant qu'une roue touche, c'est le steer au sol qui prend Q/D ; dès que la caisse quitte le sol, c'est le yaw en l'air qui prend le relais (instantanément, sans transition).

---

## Ce qui a été fait

### Depuis le commit `0a0aa00` (état de base)

Le `VehicleController` d'origine était un `MonoBehaviour` pur, single-player. Les modifications pour le rendre multijoueur :

| Fichier | Avant | Après |
|---|---|---|
| `VehicleController.cs` | `MonoBehaviour` solo | `NetworkBehaviour` (client authority) + garde solo via `IsOffline()`. Le pipeline physique (ClampSpeed, HandleSteering, HandleBraking) est strictement identique à l'original. **Puis ajout d'un `HandleAirControl()`** (torque pitch/yaw en l'air uniquement, AddTorque sur axes locaux, garde via `Wheel.IsGrounded`). **Puis ajout d'un input-lock race** (abonnement à `Race.Input.Locked`, garde `_inputLocked` en tête de `FixedUpdate`). |
| `Wheel.cs` | Inchangé | Ajout d'un getter public `IsGrounded` exposé en lecture seule, zéro dépendance réseau. Wheel ne fait toujours rien d'autre que de la friction latérale + frein. |
| `Suspension.cs` | Inchangé | Inchangé (zéro dépendance réseau) |
| `SoapboxNetworkManager.cs` | N'existait pas | `NetworkManager` custom : spawn à partir de `SoapboxSpawnPoint`, fallback cercle, auto-pickup de `FizzySteamworks`. **Puis ajout d'un host-only input** : touche Espace → `RaceManager.RequestStartRace()`. |
| `SoapboxSpawnPoint.cs` | N'existait pas | Marker de spawn avec auto-registre, support team tag, gizmo visuel |
| `CameraRig.cs` | `TestCameraRig.cs`, single-player | Suit le `NetworkClient.localPlayer` quand il existe, fallback `_editorFollowTarget` pour le mode éditeur |
| `Race/*.cs` | N'existait pas | Système de course complet (event-based) : `EventManager` hub, `Checkpoint`/`FinishLine` triggers, `VehicleCheckpointTracker` sur prefab véhicule, `CheckpointManager` ledger + classement, `CountdownManager` 3-2-1-GO, `RaceManager` state machine, `RaceHUD` UGUI auto-construit. Aucun composant Car/Wheel/Suspension n'a été modifié par ce système. |
| `NetworkOwnershipGate.cs` | N'existait pas | **Créé puis supprimé** : dead code, la logique est centralisée dans `VehicleController.OnStartClient` |

### Bug notables corrigés en cours de route

1. **Solo cassé par `isOwned` à `false`** : sans NetworkIdentity, `isOwned` reste false → le garde `if (!isOwned) return;` court-circuitait toute la simulation en solo. Corrigé par `IsOffline()` qui bypass le garde.
2. **`CacheNeutralRotations` jamais appelé en solo** : initialement déplacé dans `OnStartLocalPlayer`, qui n'est jamais appelé par Mirror en solo. Remis dans `Awake()`.
3. **Steering rotation appliquée en `Update()` au lieu de `FixedUpdate`** : provoquait un frame de retard sur la friction latérale, véhicule mou. Remis dans `FixedUpdate` comme l'original.

---

## Système de course

### Concept & flux

Sprint race : chaque joueur traverse N checkpoints dans l'ordre puis la ligne
d'arrivée. Le premier arrivé gagne. State machine :

```
Lobby → Countdown (3-2-1-GO) → Racing → Finished → (auto-restart après 10s) → Lobby
```

Le host déclenche le départ via **Espace** (côté serveur uniquement). Tous les
clients reçoivent le même countdown synchronisé (autorité serveur). À la fin,
le classement final s'affiche, puis la course redémarre automatiquement.

### Architecture : Event Bus

Tout le système communique via un **hub d'événements statique et générique**
(`Soapbox.Race.EventManager`). Aucun composant ne référence directement un
autre sauf via `EventManager.Subscribe(...)` / `EventManager.Publish(...)`.

| Clé d'event | Payload | Émis par | Consommé par |
|---|---|---|---|
| `Race.Countdown.Tick` | `int` | `CountdownManager` | `RaceHUD` |
| `Race.Countdown.Go` | — | `CountdownManager` | `RaceHUD`, `VehicleController` |
| `Race.Countdown.Cancelled` | — | `RaceManager` | `RaceHUD` |
| `Race.Checkpoint.Crossed` | `CheckpointCrossedData` | `VehicleCheckpointTracker` (client + serveur) | `CheckpointManager` (serveur) |
| `Race.Rank.Updated` | `RankUpdatedData` | `CheckpointManager` (serveur) | `RaceHUD` |
| `Race.Vehicle.Finished` | `VehicleFinishedData` | `CheckpointManager` (serveur) | `RaceManager` |
| `Race.Input.Locked` | `bool` | `CountdownManager` | `VehicleController` |
| `Race.Game.Starting` / `Started` / `Finished` / `Restart` | divers / — | `RaceManager` | `RaceHUD` |

Tous les noms de clés sont définis en `const string` dans `RaceEventKeys.cs`
→ erreur de compilation si typo, contrairement à un enum.

### Composants

- **`Checkpoint`** (`MonoBehaviour` pur) : trigger 3D posé sur chaque CP de la
  scène. Publie `CheckpointCrossed` quand un véhicule entre. **Aucun
  `using Mirror;`** — la scène ignore complètement le réseau.
- **`FinishLine`** : sous-classe de `Checkpoint` pour la ligne d'arrivée
  (sémantique pour le level designer).
- **`VehicleCheckpointTracker`** (`NetworkBehaviour`) : vit sur le prefab
  véhicule. Le client propriétaire détecte les trigger entries localement,
  valide l'ordre (anti-triche : pas de CP #3 avant CP #1), puis envoie
  un `CmdCrossCheckpoint` au serveur qui re-valide et publie l'event.
- **`CheckpointManager`** (`MonoBehaviour`, côté serveur) : ledger par
  véhicule + classement live (re-tri toutes les 0.25s par checkpoints
  franchis + distance au prochain CP). Émet `RankUpdated` à chaque
  changement et `VehicleFinished` quand un joueur termine.
- **`CountdownManager`** (`MonoBehaviour`, autoritaire) : coroutine 3-2-1-GO
  avec `WaitForSecondsRealtime`. Émet `InputLocked=true` au début, `false`
  sur GO.
- **`RaceManager`** (`MonoBehaviour`) : state machine, déclenche
  `CountdownManager.StartCountdown` quand le host appelle
  `RequestStartRace()`, enregistre tous les véhicules à GO, attend le
  premier finisher, publie les résultats, programme le restart auto.
- **`RaceHUD`** (`MonoBehaviour`, UGUI) : auto-construit un Canvas complet
  (décompte centré, position en haut-droite, temps écoulé, progression CP,
  panel classement final avec countdown restart). Auto-setup → aucune
  configuration manuelle nécessaire pour un playtest.

### Modifications de l'existant

- **`VehicleController.cs`** : +1 champ `_inputLocked`, +1 abonnement à
  `Race.Input.Locked` dans `Awake/OnDestroy`, +1 garde dans `FixedUpdate`
  (~15 lignes au total). Aucun couplage : le contrôleur ne sait pas ce
  qu'est un countdown.
- **`SoapboxNetworkManager.cs`** : +1 champ `_raceManager`, +1 `Update()`
  qui appelle `RequestStartRace()` sur Espace côté host uniquement
  (~15 lignes).

### Mode solo

`RaceManager.RequestStartRace()` et `CountdownManager.StartCountdown()`
fonctionnent en offline (`!NetworkServer.active && !NetworkClient.active`).
Lancer une scène sans NetworkManager et appuyer Espace démarre quand même
un countdown. `VehicleCheckpointTracker` publie l'event directement sans
passer par `[Command]`.

### Setup Editor (à faire une fois)

1. **Prefab véhicule** `Assets/02_Prefabs/Vehicle/Test Vehicle.prefab` :
   - `Rigidbody` : non-kinematic, `interpolation = Interpolate`
   - Ajouter `Mirror.NetworkIdentity`
   - Ajouter `Mirror.Components.NetworkTransformReliable`, `syncDirection = ClientToServer`
   - Sur le `VehicleController`, renseigner le tableau `_suspensions` avec les 4 Suspensions (FR, FL, RR, RL)
   - Les tableaux `_steeringWheels` et `_brakeWheels` sont déjà câblés d'origine
   - Ajouter `VehicleCheckpointTracker` (à côté de `VehicleController`)

2. **Scène de jeu** (par ex. `JhiderScene.unity`) :
   - Placer un `NetworkManager` (le projet en a un). Remplacer le script par `Soapbox.Networking.SoapboxNetworkManager` (subclass).
   - Assigner le prefab véhicule dans `Player Prefab` du `NetworkManager`.
   - Placer 1 à N `Soapbox.Networking.SoapboxSpawnPoint` (GameObject vide + composant) à chaque point de spawn souhaité.
   - Une `CameraRig` dans la scène (ou utiliser celle déjà présente, renommée de `TestCameraRig`).

3. **Système de course** :
   - GameObject vide `Race` dans la scène, ajouter `CountdownManager`, `CheckpointManager`, `RaceManager`. Les références inter-composants se résolvent via `FindFirstObjectByType` à l'Awake.
   - Canvas + `RaceHUD`. Le HUD construit lui-même ses textes, countdown, et panel leaderboard — aucune autre config requise.
   - Placer un `Checkpoint` (avec Collider trigger) à chaque point de contrôle, **dans l'ordre** : `_index` 0, 1, 2, …, N-1.
   - Placer un `FinishLine` (sous-classe de `Checkpoint`, flag `_isFinishLine` déjà activé) à la ligne d'arrivée.

4. **Steam** :
   - L'app ID Steam doit être configuré dans `FizzySteamworks` (transport sur le NetworkManager).
   - Chaque joueur doit avoir lancé Steam et le jeu pour que le transport fonctionne.

---

## Menu / Lobby (3-scene flow)

### Concept

Trois scènes distinctes, séparées par `NetworkManager.ServerChangeScene`
(la seule façon correcte de changer de scène pendant une session Mirror active) :

```
[Scène Menu]  --HOST-->  [Scène Lobby]  --LANCER-->  [Scène Race = JhiderScene]
       |
       --Join via Steam overlay-->  (Mirror replicate) → [Scène Lobby]
```

- **Menu** : écran titre, 3 boutons (HOST / INVITER UN AMI / QUITTER).
  HOST déclenche `SteamLobbyManager.HostLobby()` → `OnLobbyCreated` →
  `StartHost()` + `ServerChangeScene("Lobby")`. INVITER UN AMI ouvre
  l'overlay Steam (`SteamFriends.ActivateGameOverlayInviteDialog`) et n'est
  actif que quand on est déjà dans un lobby (le bouton est `interactable = false`
  sinon).
- **Lobby** : liste des joueurs connectés (lecture de `NetworkServer.connections`
  côté host, `NetworkClient.isConnected` côté guest), bouton **LANCER LA COURSE**
  (host only) qui déclenche `ServerChangeScene("JhiderScene")`, bouton
  **QUITTER LE LOBBY** qui appelle `SceneFlow.LoadMenuAfterShutdown()` (stoppe
  Mirror, puis `SceneManager.LoadScene("Menu")` local).
- **Race** : `JhiderScene.unity` (déjà câblée avec checkpoints, race manager).

### Architecture

```
Assets/01_Scripts/Menu/
├── MenuController.cs              ← Orchestrateur Menu (MonoBehaviour, 0 réseau)
├── LobbyController.cs             ← Orchestrateur Lobby (Mirror-aware, polling)
├── SceneFlow.cs                   ← Static facade : ServerChangeScene / Stop+Load
├── MenuUIBuilder.cs               ← Auto-build Canvas Menu (dark grey, 0 prefab)
├── LobbyUIBuilder.cs              ← Auto-build Canvas Lobby
├── MenuInputAdapter.cs            ← Pont EventManager -> Steam/Mirror/Scene
└── MenuInput.cs                   ← InputActionMap "Menu" (Submit/Cancel/Navigate)
```

### Event bus (EventManager — extension)

Le hub statique existant (`OnRaceCountdownTick`, etc.) a été étendu — pas
réécrit — avec deux sections :

```csharp
#region Menu System
public static event Action OnHostRequested;
public static event Action OnInviteFriendRequested;
public static event Action OnQuitRequested;
public static event Action OnReturnToMenuRequested;
public static event Action OnStartRaceRequested;
#endregion
#region Lobby System
public static event Action<int, int, string> OnLobbyRosterChanged;        // (cur, max, formatted)
public static event Action<bool> OnSteamLobbyAvailabilityChanged;
#endregion
```

### Séparation des responsabilités

- **`MenuController` / `LobbyController`** : publient des intents via
  `EventManager` mais ne touchent jamais Steam ni Mirror directement. Trivial
  à tester, trivial à swap pour un autre backend.
- **`MenuInputAdapter`** : seul fichier à connaître Steamworks / Mirror côté
  menu. Traduit les events en appels concrets.
- **`SceneFlow`** : static facade pour les transitions de scène. Centralise
  la règle "ServerChangeScene pour le réseau, SceneManager.LoadScene après
  shutdown pour le retour menu local".
- **`MenuUIBuilder` / `LobbyUIBuilder`** : construction 100% par code, pas de
  prefab à câbler. Si demain un designer veut un visuel custom, il remplace
  l'auto-build par un prefab sans toucher au contrôleur.

### Modifications minimales de l'existant

- `EventManager.cs` : +25 lignes (events Menu/Lobby). Zéro suppression.
- `SteamLobbyManager.cs` : +1 champ `CurrentLobbyId`, +1 méthode
  `OnDisconnected()`, +3 lignes dans `OnLobbyCreated` / `OnLobbyEntered`
  pour pinger la dispo du lobby. Zéro changement de comportement existant.
- `SoapboxNetworkManager.cs` : +2 overrides (`OnClientDisconnect` /
  `OnStopHost`) qui forwardent vers `SteamLobbyManager.OnDisconnected()`.
  Pure plumbing, zéro changement de logique réseau.

### Setup Editor pour les scènes Menu / Lobby

Ces scènes doivent être créées dans l'éditeur Unity (les fichiers `.unity`
ne se patchent proprement que par l'éditeur). Procédure **1 clic** via le
script editor inclus, ou procédure manuelle pour plus de contrôle.

#### Option A — 1 clic (recommandé)

Menu Unity : **Tools → Soapbox → Setup Menu & Lobby (full)**

Le script `Assets/01_Scripts/Menu/Editor/SoapboxSetup.cs` fait tout :

1. Crée / remplace `Assets/04_Scenes/Menu.unity` et y instancie le prefab
   `Assets/02_Prefabs/Manager/NetworkManager.prefab`. Ce prefab porte déjà
   `SoapboxNetworkManager` (avec `dontDestroyOnLoad: 1`), `SteamLobbyManager`,
   `MultiplexTransport` (Steam + KCP), `KcpTransport`, `FizzySteamworks`,
   `SteamManager`, le `NetworkManagerHUD` Mirror legacy, et les bons
   `offlineScene` / `onlineScene` (Menu / Lobby).
2. Ajoute un GameObject `MenuController` séparé (avec `MenuUIBuilder` auto
   via `[RequireComponent]`, plus `MenuInputAdapter`). Ce GameObject est
   `DontDestroyOnLoad` au runtime (via `MenuController.Awake()`) — il survit
   donc à la transition Menu → Lobby et permet au host de cliquer
   **LANCER LA COURSE** depuis le Lobby.
3. Appelle `MenuUIBuilder.Build()` pour générer le Canvas + boutons, et
   sauvegarde la scène (le Canvas devient un GameObject normal éditable).
4. Crée `Assets/04_Scenes/Lobby.unity` avec un GameObject `LobbyController`
   (sans NetworkManager — c'est celui de Menu qui gère la session).
5. Configure les Build Settings (Menu en index 0, Lobby en 1, JhiderScene
   en 2).
6. Affiche une confirmation, puis tu peux presser Play.

> Le prefab contient aussi un `NetworkManagerHUD` legacy qui affiche des
> boutons HOST / CLIENT / STOP. Tu peux le décocher dans l'inspecteur de
> l'instance si tu ne veux pas le voir superposé au menu custom.

Sous-commandes utiles (Tools → Soapbox → ...) :
- `Setup Menu scene only` / `Setup Lobby scene only` : recrée une seule scène.
- `Configure Build Settings` : reconfigure les scenes sans toucher aux fichiers.

#### Option B — procédure manuelle

1. **Créer `Assets/04_Scenes/Menu.unity`** (File → New Scene → vide).
   - Ajouter un GameObject `NetworkManager` avec :
     - `Mirror.NetworkManager` script → remplacer par `SoapboxNetworkManager`
     - `FizzySteamworks` (transport) sur le même GameObject
     - `Soapbox.Networking.SteamLobbyManager` sur le même GameObject
     - `Soapbox.Menu.MenuInputAdapter` sur le même GameObject
     - `Soapbox.Menu.MenuController` sur le même GameObject
   - Sauvegarder.

2. **Créer `Assets/04_Scenes/Lobby.unity`** (même GameObject `NetworkManager`
   avec en plus `Soapbox.Menu.LobbyController`).

3. **Build Settings** (File → Build Settings) : ajouter `Menu.unity`,
   `Lobby.unity`, `JhiderScene.unity` avec `Menu` en index 0.

4. **Build initial du Canvas** (à faire **une fois** par scène) :
   - Sélectionne le GameObject `NetworkManager` dans `Menu.unity`.
   - Dans l'inspecteur du composant `MenuController`, repère le sous-composant
     `MenuUIBuilder` (ajouté automatiquement via `[RequireComponent]`).
   - Clique droit sur l'en-tête du `MenuUIBuilder` → **"Build Menu UI"**.
   - Le Canvas + EventSystem + tous les boutons sont créés, et **la scène
     est sauvegardée automatiquement**. Tu peux maintenant sélectionner
     n'importe quel bouton dans la hiérarchie, le déplacer, changer
     sa couleur, son texte, etc. — tout est éditable normalement.
   - Fais pareil dans `Lobby.unity` avec le composant `LobbyUIBuilder` →
     "Build Lobby UI".

5. **Test rapide** :
   - Lancer en éditeur (play) — la scène Menu doit s'afficher.
   - Cliquer **HOST STEAM** — Steam crée un lobby, la scène bascule sur Lobby.
   - Cliquer **INVITER UN AMI** (dans le Lobby) — l'overlay Steam s'ouvre.
   - Cliquer **HOST LAN** — démarre un host sans Steam, utile pour playtest
     en local. Les clients se connectent via **REJOINDRE EN LAN** (panel
     modal qui demande l'IP).
   - Build standalone, host dans l'éditeur → inviter l'ami depuis Steam →
     le guest arrive dans la même scène Lobby.

### Modes de connexion (Steam + LAN)

Le menu propose trois entrées distinctes, toutes routées via `EventManager` :

| Bouton | Où | Comportement |
|---|---|---|
| **HOST STEAM** | Menu | Crée un lobby Steam (FriendsOnly), `StartHost()`, transition vers Lobby. Invite via l'overlay Steam. |
| **HOST LAN** | Menu | `StartHost()` direct, sans Steam. Utilise le transport Mirror de base (UDP). Bascule vers Lobby. |
| **REJOINDRE EN LAN** | Modal (ouvert depuis le Menu via... voir ci-dessous) | Panel InputField IP, `StartClient()` vers l'IP saisie. IP mémorisée (PlayerPrefs). |

> Note : pour **rejoindre une partie Steam**, c'est l'overlay Steam
> (Shift+Tab) qui gère — pas de bouton custom. La Menu affiche un hint
> statique pour le rappeler au joueur.

### Le Canvas auto-construit est éditable

Le pattern `BuildIfNeeded()` + `Build()` (marqué `[ContextMenu]`) fait que :

- **Premier ajout du `MenuController`** → `BuildIfNeeded()` détecte qu'il
  n'y a pas encore de Canvas → appelle `Build()` → crée tout et **save
  la scène**.
- **F5 / play ultérieurs** → `BuildIfNeeded()` voit que `_built == true`
  (champs sérialisé) → ne reconstruit rien. Le Canvas survit.
- **Modifications manuelles** → tu changes la couleur d'un bouton dans
  l'inspecteur, Ctrl+S, et ça reste.
- **Tu casses le Canvas par erreur** → re-clique "Build Menu UI" sur
  l'inspecteur pour tout reconstruire (attention : ça écrase tes modifs).

### Limitations connues du lot 2

- **Rejoindre via Steam** = passer par l'overlay Steam (pas de liste
  custom). Le hint statique du Menu le rappelle.
- **Pas de ready check, pas de chat, pas de kick, pas de team.**
- Le scénario nominal : host clique HOST STEAM → lobby créé +
  `ServerChangeScene` immédiat. Si un guest accepte une invitation APRÈS
  le SceneChange, il rejoint dans la scène courante (Mirror gère la
  synchro), UX non raffinée.
- Le roster ne montre pas encore les noms Steam (juste `Joueur <connectionId>`).
- **HOST LAN** suppose que tu utilises un transport Mirror UDP de base
  (KCP / Telepathy) sur le NetworkManager. Si tu veux du LAN discovery
  automatique (zéro saisie IP), il faudra ajouter `Mirror.Discovery`
  dans un lot suivant.

---

## Pour la prochaine IA / le prochain dev

> **Tu es sur un projet Unity multijoueur. Avant d'écrire la moindre ligne, lis ce README en entier, puis le header de `VehicleController.cs` (qui documente les décisions d'architecture). Respecte les règles ci-dessous.**

### 1. Lis le code existant avant de modifier

Le code est petit (3 fichiers Car + 2 fichiers Network + 1 fichier Camera). Lis tout. Les commentaires en tête de `VehicleController.cs` documentent le *pourquoi* des choix (pas juste le *quoi*). Si tu proposes un changement qui contredit un de ces commentaires, **tu dois mettre à jour le commentaire en même temps**.

### 2. Préserve la séparation des responsabilités

- `Wheel.cs` et `Suspension.cs` doivent **rester 100 % ignorants du réseau**. Pas de `using Mirror;`, pas de `NetworkBehaviour`, pas de référence à `NetworkIdentity`. Si tu as besoin d'un comportement réseau dans la physique, **pilote Wheel / Suspension depuis l'extérieur** (comme le fait `VehicleController.SetWheelsEnabled`).
- `VehicleController.cs` est le seul point d'entrée pour la logique réseau. Ne le contourne pas.

### 3. Préserve le mode solo

Toute modification réseau doit garder un chemin solo fonctionnel (scène `Test Vehicle.unity` ou `Test Hill.unity` lancées sans NetworkManager). Si tu ajoutes un garde `if (!isOwned) return;`, ajoute **toujours** `&& !IsOffline()` derrière, ou encapsule-le dans un helper du style `ShouldSimulate()`.

### 4. Pas de dead code

Si tu crées un helper, un composant, ou un ScriptableObject, **et qu'il n'est plus utilisé**, supprime-le. Pas de "au cas où on en aurait besoin plus tard". Le code mort coûte plus cher qu'un re-création (lecture, confusion, maintenance).

Vérifie les références avant de supprimer : `grep` du nom de la classe dans `Assets/01_Scripts/`.

### 5. Pipeline physique : le `FixedUpdate` est sacré

`Wheel.cs` lit `transform.right` et `transform.forward` **dans son propre `FixedUpdate`**. Pour que la friction latérale et le frein fonctionnent :

- `VehicleController.HandleSteering()` doit appliquer la rotation aux Transform des steering wheels **dans le même FixedUpdate** que le calcul de `_currentSteerAngle`. Pas en `Update()`. Pas en `LateUpdate()`. En `FixedUpdate()`, point.
- Ne rajoute **pas** de composants type `PredictedRigidbody` ou autre interférence sur la transform du véhicule (sauf si tu repasses en server authority, auquel cas il faut documenter le pourquoi).

### 6. Defaults inspector = defaults code

Quand tu modifies un champ `[SerializeField]` dans `VehicleController.cs`, mets un défaut **cohérent avec ce qui est sérialisé dans le prefab**. Si le prefab override à 10°/3/30 et que tu changes le défaut à 50°/5/30, **toute nouvelle instance de prefab** partira avec 50°/5/30. Documente la valeur dans le tooltip.

### 7. Commentaires = pour le prochain qui passe

Un commentaire qui dit *ce que le code fait* est inutile (le code le dit). Un commentaire qui dit *pourquoi* (la décision, le tradeoff, le piège évité) est précieux. Les en-têtes XML (`///`) sur les classes et méthodes publiques sont attendus par convention dans ce projet.

### 8. Mets à jour ce README

Si tu ajoutes un système, change l'architecture, ou inverses une décision documentée ici, **mets à jour ce README dans le même commit**. Le README est la source de vérité de l'intention du projet — pas le code (le code change, l'intention doit rester lisible).

### 9. Avant de commit

Checklist minimale :

- [ ] `git status` propre (pas de fichier généré par Unity qui ne devrait pas être versionné — vérifie `.gitignore`)
- [ ] Le projet compile sans warning (ouvre la console Unity, pas seulement ton IDE)
- [ ] Le mode solo fonctionne toujours (lance `Test Vehicle.unity` sans NetworkManager)
- [ ] Le mode multi fonctionne (build standalone, host + 1 client, vérifier qu'on peut rejoindre et que chaque joueur contrôle sa voiture)
- [ ] Le feeling de steering n'a pas changé (si tu as touché au pipeline physique, sanity check)
- [ ] Le README est à jour

### 10. Demande avant de régresser

Si tu dois toucher à un truc qui va clairement empirer le feeling ou la séparation des responsabilités (ex : repasser en server authority, fusionner `VehicleController` + `Wheel`, virer le mode solo), **documente la raison dans un commentaire `// WHY: ...` au-dessus du changement** et idéalement annonce-le dans la PR / le message de commit. Le prochain dev qui passera doit pouvoir défaire ta décision sans devoir tout ré-analyser.

---

## Pistes d'amélioration futures (non faites)

- **Server authority** (si mode compétitif) : `VehicleController` uniquement. Wheel / Suspension restent intacts.
- **Plus de véhicules** : factoriser les paramètres véhicule dans un `ScriptableObject` (le slot existe déjà : `Scriptable Scripts/Wheel Data.cs`, à généraliser).
- **Power-ups / armes** : la physique est déjà compatible, à brancher via un nouveau composant sur le prefab véhicule.
- **Lobby Steam** : `FizzySteamworks` gère déjà l'auth, manque l'UI pour créer / rejoindre des parties.

---

## Crédits

- Mirror : https://mirror-networking.com (MIT)
- Fizzy Steamworks : https://github.com/Chykary/FizzySteamworks
- Steamworks.NET : https://github.com/rlabrecque/Steamworks.NET
- NaughtyAttributes : https://github.com/dbrizov/NaughtyAttributes
- Universal Render Pipeline : Unity Technologies
