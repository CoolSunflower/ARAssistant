/**
 * Cloudflare Worker: LLM backend (non-stream + streaming SSE)
 * Endpoints:
 *   POST /chat        -> returns plain text
 *   GET  /chat-sse?q= -> streams chunks with SSE (data: "<delta>\n\n", ends with data: [DONE])
 *   GET  /health      -> 200 OK
 *
 * Notes:
 * - Uses OpenAI "Responses" API with stream:true for /chat-sse
 * - CORS is enabled for GET/POST/OPTIONS so Unity WebRequest works
 */

const CORS_HEADERS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Methods": "GET,POST,OPTIONS",
  "Access-Control-Allow-Headers": "Content-Type, Authorization",
};

export default {
  async fetch(request, env, ctx) {
    const url = new URL(request.url);
    if (request.method === "OPTIONS") return new Response(null, { headers: CORS_HEADERS });

    if (url.pathname === "/health") {
      return new Response("OK", { status: 200, headers: CORS_HEADERS });
    }

    if (url.pathname === "/chat" && request.method === "POST") {
      return handleChat(request, env);
    }

    if (url.pathname === "/chat-sse" && request.method === "GET") {
      return handleChatSSE(url, env);
    }

    return new Response("Not found", { status: 404, headers: CORS_HEADERS });
  },
};

function sysPrompt() {
  // keep this short & purposeful—Unity mobile needs concise, speakable outputs
  return [
    "You are an AR voice assistant speaking to a user through a humanoid avatar in augmented reality.",
    "Goals:",
    "1) Be concise, natural, and helpful. Prefer short sentences that sound good aloud.",
    "2) When explaining steps, use simple sequencing (First, Next, Finally).",
    "3) Avoid filler and emojis. No markdown.",
    "4) If you mention actions in the real world, keep them safe and practical.",
  ].join(" ");
}

/** ----------- Non-streaming ----------- */
async function handleChat(req, env) {
  try {
    const { text } = await req.json();
    if (!text || !text.trim()) {
      return withCORS(new Response("No text", { status: 400 }));
    }

    const body = {
      model: env.OPENAI_MODEL || "gpt-4o-mini",
      input: [
        { role: "system", content: sysPrompt() },
        { role: "user", content: text },
      ],
    };

    const r = await fetch("https://api.openai.com/v1/responses", {
      method: "POST",
      headers: {
        "Authorization": `Bearer ${env.OPENAI_API_KEY}`,
        "Content-Type": "application/json",
      },
      body: JSON.stringify(body),
    });

    if (!r.ok) {
      const errText = await r.text();
      return withCORS(new Response(errText, { status: r.status }));
    }

    const data = await r.json();
    const out = extractFinalText(data);
    return withCORS(new Response(out, {
      status: 200,
      headers: { "Content-Type": "text/plain; charset=utf-8", ...CORS_HEADERS },
    }));
  } catch (e) {
    return withCORS(new Response("Server error: " + (e?.message || e), { status: 500 }));
  }
}

/** ----------- Streaming (SSE) ----------- */
async function handleChatSSE(url, env) {
  const q = (url.searchParams.get("q") || "").trim();
  if (!q) return withCORS(new Response("Missing q", { status: 400 }));

  // Upstream request to OpenAI with stream=true
  const upstream = await fetch("https://api.openai.com/v1/responses", {
    method: "POST",
    headers: {
      "Authorization": `Bearer ${env.OPENAI_API_KEY}`,
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      model: env.OPENAI_MODEL || "gpt-4o-mini",
      input: [
        { role: "system", content: sysPrompt() },
        { role: "user", content: q },
      ],
      stream: true,
    }),
  });

  if (!upstream.ok || !upstream.body) {
    const e = await upstream.text();
    return withCORS(new Response("Upstream error: " + e, { status: 502 }));
  }

  const stream = new ReadableStream({
    async start(controller) {
      const enc = new TextEncoder();
      const dec = new TextDecoder();
      const reader = upstream.body.getReader();
      let buffer = "";

      // heartbeat every 25s to keep some networks alive
      const heartbeat = setInterval(() => {
        controller.enqueue(enc.encode(": ping\n\n"));
      }, 25000);

      const send = (data) => controller.enqueue(enc.encode(`data: ${data}\n\n`));

      const flush = () => {
        const lines = buffer.split(/\r?\n/);
        buffer = lines.pop() || "";
        for (const ln of lines) {
          if (!ln.startsWith("data:")) continue;
          const payload = ln.replace(/^data:\s?/, "").trim();
          if (payload === "[DONE]") {
            send("[DONE]");
            clearInterval(heartbeat);
            controller.close();
            return true;
          }
          try {
            const obj = JSON.parse(payload);
            const delta = extractDeltaText(obj);
            if (delta) send(JSON.stringify(delta)); // JSON-escaped string chunk
          } catch {
            // ignore non-JSON lines
          }
        }
        return false;
      };

      while (true) {
        const { value, done } = await reader.read();
        if (done) break;
        buffer += dec.decode(value, { stream: true });
        const finished = flush();
        if (finished) return;
      }
      if (buffer.length) flush();
      send("[DONE]");
      clearInterval(heartbeat);
      controller.close();
    },
  });

  return withCORS(new Response(stream, {
    status: 200,
    headers: {
      "Content-Type": "text/event-stream; charset=utf-8",
      "Cache-Control": "no-cache",
      "Connection": "keep-alive",
      ...CORS_HEADERS,
    },
  }));
}

/** Helpers */
function withCORS(res) {
  const h = new Headers(res.headers);
  for (const [k, v] of Object.entries(CORS_HEADERS)) h.set(k, v);
  return new Response(res.body, { status: res.status, headers: h });
}

// Try to be robust to different response shapes
function extractFinalText(data) {
  // Preferred shortcut
  if (typeof data.output_text === "string") return data.output_text;

  // Try typical content shape
  if (Array.isArray(data.content) && data.content.length) {
    const c0 = data.content[0];
    if (typeof c0.text === "string") return c0.text;
    if (Array.isArray(c0) && c0[0]?.text) return c0[0].text;
    if (c0?.content && typeof c0.content === "string") return c0.content;
  }
  // Fallback
  try { return JSON.stringify(data); } catch { return ""; }
}

// Pull a text delta from a streaming event object
function extractDeltaText(obj) {
  // Known/likely fields for text deltas across response types:
  if (typeof obj.output_text_delta === "string") return obj.output_text_delta;
  if (typeof obj.delta === "string") return obj.delta;
  if (typeof obj.text === "string") return obj.text;

  // Sometimes nested structures appear; try a few heuristics:
  if (obj?.content && typeof obj.content === "string") return obj.content;
  if (Array.isArray(obj?.content) && obj.content.length && typeof obj.content[0]?.text === "string") {
    return obj.content[0].text;
  }
  return "";
}
