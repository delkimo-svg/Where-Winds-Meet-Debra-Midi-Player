# Create the GitHub repository (your account)

Repository name (recommended): **`Where-Winds-Meet-Debra-Midi-Player`**  
Display title: **Where Winds Meet Debra MIDI Player — full songs catalogue**

---

## Step 1 — Install GitHub CLI (optional but fastest)

```powershell
winget install GitHub.cli
```

Close and reopen PowerShell, then:

```powershell
gh auth login
```

Choose: GitHub.com → HTTPS → Login in browser.

---

## Step 2 — One-command setup (after `gh auth login`)

From the project root:

```powershell
cd C:\Users\Utilisateur\Projects\WhereWindsMeetMidiPlayer
.\scripts\setup-github-repo.ps1
```

This will:

- Verify `discord-catalogue.json` is **not** staged (secrets)
- Create the initial commit
- Create a **public** repo on your account
- Push `master` branch

---

## Step 2 (alternative) — GitHub website

1. Open https://github.com/new
2. **Repository name:** `Where-Winds-Meet-Debra-Midi-Player`
3. **Description:** `Where Winds Meet Debra MIDI Player with full songs catalogue — portable Windows player, live Discord library`
4. **Public**
5. Do **not** add README / license (already in project)
6. Create repository

Then in PowerShell:

```powershell
cd C:\Users\Utilisateur\Projects\WhereWindsMeetMidiPlayer

git add .
git status   # confirm discord-catalogue.json is NOT listed
git commit -m "Debra MIDI Player v1.0 — portable player with Discord catalogue"

git branch -M main
git remote add origin https://github.com/YOUR_USERNAME/Where-Winds-Meet-Debra-Midi-Player.git
git push -u origin main
```

Replace `YOUR_USERNAME` with your GitHub login.

---

## Step 3 — GitHub Release (v1.0 download link)

Large portable ZIP (~69 MB) — use **GitHub Releases** (good for `-DownloadUrl` in Discord publish).

```powershell
# Build + zip (if not done)
.\scripts\build-release.ps1 -Target portable
# Create release\DebraMidiPlayer-1.0.0-portable.zip with 7-Zip from release\portable\*

.\scripts\publish-github-release.ps1 -Version 1.0.0
```

Or on the website: repo → **Releases** → **Draft a new release** → tag `v1.0.0` → upload the ZIP.

Copy the **asset download URL** and publish to Discord:

```powershell
.\scripts\publish-release-to-discord.ps1 -SkipBuild -Version 1.0.0 `
  -DownloadUrl "https://github.com/YOUR_USERNAME/Where-Winds-Meet-Debra-Midi-Player/releases/download/v1.0.0/DebraMidiPlayer-1.0.0-portable.zip" `
  -NotesFile .\RELEASE_NOTES_1.0.0.md -UpdateConfig
```

---

## Step 4 — Visibility on GitHub

After push, on the repo page → **⚙ Settings** → **General**:

- **Description:** same as above
- **Topics:** `where-winds-meet`, `midi`, `debra`, `discord`, `wpf`, `csharp`, `portable`, `game-music`
- **Social preview:** upload a 1280×640 image (logo + title)

Pin the repo on your profile: Profile → **Customize pins**.

---

## Security checklist

- [ ] `discord-catalogue.json` never committed (in `.gitignore`)
- [ ] No bot token in commit history (`git log -p` if unsure)
- [ ] Rotate bot token if it was ever pasted in chat or committed
