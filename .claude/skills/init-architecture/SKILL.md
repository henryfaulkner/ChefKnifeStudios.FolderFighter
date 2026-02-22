# Skill: Directory Battle Engine

## 1. Project Overview: "The File System is the Game"
**Directory Battle** is a real-time multiplayer game that uses the operating system's native file explorer (Windows File Explorer or Mac Finder) as the primary user interface. 

Instead of clicking buttons in a graphical window, players cast spells by dragging and dropping actual text files across directories on their hard drive. A backend .NET Core console application runs quietly in the background, watching these file movements, syncing state with other players via Azure Event Hubs, and logging the battle to the terminal.

### Core Mechanics
* **The Arena:** Your current working directory acts as your base. 
* **The Opponents:** Every active enemy is represented as a sub-directory inside your base (e.g., `./Opponent_A`, `./Opponent_B`).
* **Drawing Spells:** The background service automatically generates "Spell Files" (e.g., `Fireball.txt`, `Shield.txt`) in your root directory at regular intervals.
* **Attacking:** To cast a spell at an opponent, the player drags a spell file from their root directory and drops it into an opponent's sub-directory.
* **Multi-Attack:** Players can multi-select several spell files and drag them into a folder simultaneously. 
* **Defending/Crafting:** Dropping files into a dedicated `.self` or `Forge` directory triggers defensive buffs or spell combinations.

---

## 2. System Architecture
The application is built in **C# / .NET Core** and strictly follows the **Elm Architecture (TEA)**. State is immutable, UI (Console) is a pure projection of the state, and side effects (File System, Network) are handled via message passing.



### Tech Stack
* **Language:** C# 10+ / .NET Core (Worker Service)
* **State Management:** Elm-style immutable records and pattern matching
* **Networking:** Azure Event Hubs (`Azure.Messaging.EventHubs`)
* **I/O:** `System.IO.FileSystemWatcher`

---

## 3. The Elm Architecture Implementation

### A. The Model (State)
The system state is immutable. It represents the source of truth for the local player's instance.

```csharp
public record GameState(
    string PlayerName,
    int Health = 100,
    Dictionary<string, int> Opponents = new(), // OpponentName -> Health
    Queue<string> CombatLogs = new()
);
B. The Messages (Actions)
Messages describe every possible event that can occur in the system, whether triggered by the player (moving a file), the network (getting attacked), or the system (tick to draw a spell).

C#
public abstract record Msg;

// I/O Driven Messages (File System)
public record SpellCast(string SpellName, string TargetName) : Msg;

// Network Driven Messages (Azure Event Hub)
public record SpellReceived(string Attacker, string SpellName, int Damage) : Msg;
public record OpponentJoined(string OpponentName) : Msg;

// Timer Driven Messages
public record TickDrawSpell(string SpellName) : Msg;
C. The Update Function
The core game logic. It takes the current state and a message, returning a brand new state. No side effects occur here.

C#
public static GameState Update(Msg msg, GameState state) => msg switch
{
    SpellCast s => state with { 
        CombatLogs = EnqueueLog(state.CombatLogs, $"✨ You cast {s.SpellName} on {s.TargetName}") 
    },
    
    SpellReceived r => state with { 
        Health = state.Health - r.Damage,
        CombatLogs = EnqueueLog(state.CombatLogs, $"💥 {r.Attacker} hit you with {r.SpellName}!") 
    },
    
    OpponentJoined o => state with {
        Opponents = AddOpponent(state.Opponents, o.OpponentName, 100),
        CombatLogs = EnqueueLog(state.CombatLogs, $"⚔️ {o.OpponentName} entered the arena.")
    },
    
    _ => state
};
D. The View (Console)
The "View" is a simple console render loop that clears the screen and redraws the UI based only on the current GameState.

Plaintext
====================================
 PLAYER: User_Alpha      HP: [90/100]
====================================
 ENEMIES:
 - User_Bravo            HP: [100/100]
 - User_Charlie          HP: [40/100]

 COMBAT LOG:
 > ⚔️ User_Charlie entered the arena.
 > ✨ You cast Fireball on User_Bravo.
 > 💥 User_Charlie hit you with Ice_Bolt!
====================================
4. Side Effects & Integrations
To keep the Elm Update loop pure, we handle side effects in wrapper services that map external events into Msg objects.

FileSystemWatcher (Local Inputs)
Monitors the working directory.

Logic: When an OnRenamed or OnMoved event fires, the watcher checks if the destination is an Opponent directory.

Action: If yes, it physically deletes the file (consuming the spell) and dispatches a SpellCast message to the Elm loop.

Azure Event Hub (Multiplayer Sync)
Producer: When the Elm loop processes a SpellCast, a side-effect function serializes the action and pushes it to Azure.

Consumer: An EventProcessorClient listens for incoming events. When an event's target matches the local player, it dispatches a SpellReceived message to the Elm loop.

Event Payload Schema (JSON):

JSON
{
  "type": "Attack",
  "attacker": "User_Alpha",
  "target": "User_Bravo",
  "spell": "Fireball.txt",
  "damage": 10,
  "timestamp": "2026-02-21T18:42:00Z"
}
Directory Management (Output Sync)
When the GameState adds a new opponent, a side-effect service automatically triggers Directory.CreateDirectory("./User_Bravo"). If an opponent dies or leaves, the directory is deleted.


Would you like me to write the C# boilerplate for the `FileSystemWatcher` service so you can see exactly how to translate physical file drops into Elm messages?