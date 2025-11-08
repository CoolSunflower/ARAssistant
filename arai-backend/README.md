## Instructions for hosting backend on cloudflare
1. Setup all node_modules packages `npm i`
2. `wrangler login` and login to cloudflare
3. `wrangler secret put OPENAI_API_KEY` and enter key when prompted, then press Y for creating worker as well.
4. `wrangler deploy` and use the generated HTTPS URL in Unity application.