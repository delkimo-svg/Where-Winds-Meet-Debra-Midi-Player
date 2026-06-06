# First time on GitHub — complete walkthrough

This guide assumes **zero GitHub experience**. Do the steps **in order**.

---

## What you are doing (simple picture)

| Step | What happens |
|------|----------------|
| 1 | Tell Git your name (on your PC) |
| 2 | Save a **snapshot** of the project (commit) |
| 3 | Create an empty **repository** on GitHub (website) |
| 4 | **Upload** your snapshot (push) |
| 5 | Attach the **game ZIP** as a Release (download link for players) |
| 6 | Tell Discord the GitHub download link (optional) |

**Important:** `discord-catalogue.json` (bot password) stays **only on your PC**. It is **not** uploaded to GitHub. Players get it inside the **Release ZIP** you build locally.

---

## Before you start

- [ ] GitHub account: https://github.com/signup  
- [ ] Project folder: `C:\Users\Utilisateur\Projects\WhereWindsMeetMidiPlayer`  
- [ ] File exists: `discord-catalogue.json` (local only — OK)  
- [ ] Optional ZIP already built: `release\DebraMidiPlayer-1.0.0-portable.zip`

---

## Step 1 — Tell Git who you are (once per PC)

Open **PowerShell** and run (use your real GitHub email and username):

```powershell
git config --global user.email "you@example.com"
git config --global user.name "YourGitHubUsername"
```

Check:

```powershell
git config user.name
git config user.email
```

---

## Step 2 — Save the project snapshot (commit)

```powershell
cd C:\Users\Utilisateur\Projects\WhereWindsMeetMidiPlayer
```

**Safety check** — this must show **only** `discord-catalogue.json.example`, **not** `discord-catalogue.json`:

```powershell
git status --short | Select-String discord
```

Create the commit:

```powershell
git commit -m "Debra MIDI Player v1.0 - portable player with Discord catalogue"
git branch -M main
```

---

## Step 3 — Create the repository on GitHub (website)

1. Open https://github.com/new  
2. **Owner:** your account  
3. **Repository name:** `Where-Winds-Meet-Debra-Midi-Player`  
4. **Description:** `Where Winds Meet Debra MIDI Player with full songs catalogue`  
5. **Public**  
6. **Do not** check “Add a README” (you already have one)  
7. Click **Create repository**

GitHub shows a page with commands — use **Step 4** below (replace `YOUR_USERNAME`).

---

## Step 4 — Upload your code (push)

On the GitHub page, find your username in the URL, or look at your profile.

```powershell
cd C:\Users\Utilisateur\Projects\WhereWindsMeetMidiPlayer
git remote add origin https://github.com/YOUR_USERNAME/Where-Winds-Meet-Debra-Midi-Player.git
git push -u origin main
```

- A browser window may ask you to **log in** to GitHub (first time only).  
- When it finishes, refresh the repo page — you should see all your files and **README**.

---

## Step 5 — Make the game downloadable (Release)

GitHub **code** is not what players download — they need a **Release** with the ZIP.

### Option A — Website (easiest first time)

1. On your repo, click **Releases** (right side) → **Create a new release**  
2. **Choose a tag:** type `v1.0.0` → **Create new tag**  
3. **Release title:** `Debra Midi Player 1.0.0`  
4. **Description:** paste text from `RELEASE_NOTES_1.0.0.md`  
5. **Attach files:** drag `release\DebraMidiPlayer-1.0.0-portable.zip`  
   - If you don’t have it, build first (Step 5B)  
6. Click **Publish release**

### Option B — Build ZIP then upload

```powershell
cd C:\Users\Utilisateur\Projects\WhereWindsMeetMidiPlayer
.\scripts\build-release.ps1 -Target portable
```

Zip the **contents** of `release\portable\` (or use 7-Zip):

```powershell
& "${env:ProgramFiles}\7-Zip\7z.exe" a -tzip "release\DebraMidiPlayer-1.0.0-portable.zip" "release\portable\*"
```

Confirm the ZIP contains:

- `DebraMidiPlayer.exe`  
- `Assets\`  
- `discord-catalogue.json` ← needed for catalogue for players  

Then upload that ZIP in **Releases** (Option A).

### Copy the download link

After publish, right-click the ZIP on the release page → **Copy link address**.  
It looks like:

`https://github.com/YOUR_USERNAME/Where-Winds-Meet-Debra-Midi-Player/releases/download/v1.0.0/DebraMidiPlayer-1.0.0-portable.zip`

---

## Step 6 — Discord announcement (optional)

Post that link to Discord (bot cannot upload 69 MB files):

```powershell
.\scripts\publish-release-to-discord.ps1 -SkipBuild -Version 1.0.0 `
  -DownloadUrl "PASTE_YOUR_GITHUB_RELEASE_ZIP_URL_HERE" `
  -NotesFile .\RELEASE_NOTES_1.0.0.md -UpdateConfig
```

---

## Step 7 — Visibility

On the repo → **Settings** → scroll to **Topics** → add:

`where-winds-meet` `midi` `debra` `discord` `wpf` `game-music`

On your **GitHub profile** → **Customize your pins** → pin this repo.

---

## Troubleshooting

| Problem | Fix |
|---------|-----|
| `Author identity unknown` | Step 1 |
| `discord-catalogue.json` in `git status` | Do not commit — check `.gitignore` |
| `failed to push` / auth | Log in via browser when Git asks; or install [GitHub Desktop](https://desktop.github.com/) |
| `remote origin already exists` | `git remote set-url origin https://github.com/YOUR_USERNAME/Where-Winds-Meet-Debra-Midi-Player.git` |
| Catalogue missing in player ZIP | Rebuild portable **after** `discord-catalogue.json` exists in project root |

---

## Checklist — you are done when

- [ ] Repo visible on GitHub with README  
- [ ] Release `v1.0.0` with ZIP attached  
- [ ] You tested download + extract + run `DebraMidiPlayer.exe`  
- [ ] Catalogue refresh works in the downloaded copy  
