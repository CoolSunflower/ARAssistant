// detector-server/index.js
const express = require("express");
const bodyParser = require("body-parser");
const cors = require("cors");
const { v4: uuidv4 } = require("uuid");

const API_KEY = process.env.DETECTOR_KEY || "mysecret";
const CLAIM_TTL_MS = 60 * 1000; // 60 seconds claim lease

const app = express();
app.use(cors());
app.use(bodyParser.json());

/** Simple in-memory queue of messages (oldest first) */
let msgs = []; // each: { id, text, ts, consumed, claimedBy, claimExpiry }

function nowIso() { return new Date().toISOString(); }

/** push: detector posts new text */
app.post("/push", (req, res) => {
  const key = req.headers["x-api-key"] || req.body?.key;
  if (key !== API_KEY) return res.status(401).json({ ok: false, err: "auth" });
  const text = (req.body?.text || "").trim();
  if (!text) return res.status(400).json({ ok: false, err: "empty" });

  // optional dedupe: if last message text equals this and not consumed, ignore
  const last = msgs[msgs.length - 1];
  if (last && !last.consumed && last.text === text) {
    return res.json({ ok: true, id: last.id, dup: true });
  }

  const m = { id: uuidv4(), text, ts: nowIso(), consumed: false, claimedBy: null, claimExpiry: null };
  msgs.push(m);
  console.log("[detector] push", m.id, m.text);
  return res.json({ ok: true, id: m.id });
});

/**
 * latest: client asks for one message to process.
 * Query: ?clientId=... 
 * Server returns one available message and claims it for this client for CLAIM_TTL_MS.
 */
app.get("/latest", (req, res) => {
  const clientId = req.query.clientId || "unknown";
  // find first message not consumed and either unclaimed or whose claim expired
  const now = Date.now();
  for (let m of msgs) {
    if (m.consumed) continue;
    if (!m.claimedBy || (m.claimExpiry && now > m.claimExpiry)) {
      // claim it
      m.claimedBy = clientId;
      m.claimExpiry = now + CLAIM_TTL_MS;
      console.log(`[detector] claimed ${m.id} for ${clientId}`);
      return res.json({ id: m.id, text: m.text, ts: m.ts, claimExpiry: m.claimExpiry });
    }
  }
  // nothing available
  return res.json({ id: null, text: "", ts: null });
});

/**
 * ack: client tells server message processed (spoken) successfully
 * POST body: { key, id, clientId }
 */
app.post("/ack", (req, res) => {
  const key = req.headers["x-api-key"] || req.body?.key;
  if (key !== API_KEY) return res.status(401).json({ ok: false, err: "auth" });
  const id = req.body?.id;
  const clientId = req.body?.clientId;
  if (!id || !clientId) return res.status(400).json({ ok: false, err: "badargs" });

  const m = msgs.find(x => x.id === id);
  if (!m) return res.status(404).json({ ok: false, err: "notfound" });

  // only the client that claimed it may ack it (prevents races)
  if (m.claimedBy !== clientId) {
    // if claim expired, we might accept ack — but let's be strict: reject
    return res.status(409).json({ ok: false, err: "not-claimed-by-client", claimedBy: m.claimedBy });
  }

  m.consumed = true;
  m.claimedBy = clientId;
  m.claimExpiry = Date.now();
  console.log(`[detector] acked ${id} by ${clientId}`);
  return res.json({ ok: true });
});

/** optional endpoint to list messages (for debugging) */
app.get("/_debug/list", (req, res) => res.json(msgs));

const PORT = process.env.PORT || 5000;
app.listen(PORT, () => console.log(`detector server listening ${PORT}`));
