# How to Get Your ONE Voice Solution .exe Installer

No Visual Studio needed. GitHub builds it for you automatically — for free.

---

## One-Time Setup (takes about 10 minutes)

### Step 1 — Create a free GitHub account
Go to https://github.com and sign up if you don't have an account.

### Step 2 — Create a new private repository
1. Click the **+** button (top right) → **New repository**
2. Name it: `one-voice-solution-desktop`
3. Set it to **Private**
4. Click **Create repository**

### Step 3 — Upload the code
1. On the repository page, click **uploading an existing file**
2. Drag and drop the entire contents of the `desktop-app` folder from the ZIP
3. Scroll down, click **Commit changes**

That's it — GitHub will automatically start building your installer.

---

## Every Time You Want a New Build

### Step 4 — Download your installer
1. Go to your repository on GitHub
2. Click the **Actions** tab at the top
3. Click the latest green checkmark run (takes about 3-5 minutes to finish)
4. Scroll down to **Artifacts**
5. Click **ONENEW20260401-Installer** to download the `.exe`

---

## Making Updates in the Future

Whenever you want to update the app:
1. Edit any `.cs` file directly on GitHub (click the file → pencil icon)
2. Click **Commit changes**
3. GitHub automatically rebuilds and a new installer appears in Actions within minutes

---

## Shutting Down DigitalOcean

Once you've downloaded and tested the new installer:
1. Install it on your PC — confirm the app works and your license activates
2. Log into DigitalOcean → **Destroy** the droplet
3. Done — you'll never pay DigitalOcean again

---

## Need Help?

If the build shows a red X instead of a green checkmark:
- Click the failed run → click the failed step → read the error message
- Most common issue: a file path in the `.iss` installer script needs updating

Contact your developer with the error message and they can fix it in 5 minutes.
