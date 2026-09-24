FROM node:lts

WORKDIR /app

COPY ./agot-bg-game-server/package.json .
COPY ./agot-bg-game-server/yarn.lock .
RUN yarn install --frozen-lockfile

COPY ./agot-bg-game-server/ .

RUN yarn run generate-json-schemas

# Deliberately NOT `["yarn", "run", "run-server"]`: yarn (classic v1) is PID 1 in that form and
# does not forward SIGTERM to the ts-node child it spawns (verified: `docker stop` returns in
# under a second with the child never seeing the signal). Since the child is never notified, the
# game server's own SIGTERM handler (GlobalServer.shutdown()/flushSaveGame(), see server.ts) never
# runs before the Linux kernel kills the whole PID namespace the instant PID 1 (yarn) exits - so
# every deploy has been silently dropping whatever save was still sitting inside the 2s
# lodash.throttle window, with none of the "harden live-game save handling" commit's shutdown
# safety net ever actually executing. Invoking the ts-node binary directly makes it PID 1 itself,
# so `docker stop`'s SIGTERM reaches the process that actually installed the handler.
CMD ["node_modules/.bin/ts-node", "-T", "src/server/server.ts"]
