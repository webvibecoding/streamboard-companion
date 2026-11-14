# Privacy

Streamboard Companion monitors only the foreground Windows process while the
application is running. It uses local Steam/Epic manifests and executable
metadata to determine a display name.

The Companion may send the following to `https://streamboard.sip1ay.pro`:

- a Steam or Epic identifier, or a SHA-256 identifier derived locally for an executable;
- the detected game display name and executable file name;
- a random event identifier and observation timestamp;
- the device name supplied during pairing;
- requests to save category mappings selected by the signed-in user.

It does not upload full executable paths, window titles, process lists, browser
cookies, or Twitch/VK Play Live OAuth tokens. Platform credentials remain on the
Streamboard server. The Streamboard refresh credential is stored in Windows
Credential Manager and is never written to application logs.

Local diagnostic logs are bounded and redact authorization headers, device
codes, refresh tokens, and local paths. Uninstalling the application removes its
program files; disconnect the device first if you also want to revoke its server
credential.
