-- Core schema for Slopworks
-- Players (linked to auth.users)
CREATE TABLE IF NOT EXISTS public.players (
  id            UUID PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
  display_name  TEXT NOT NULL,
  avatar_url    TEXT,
  build_version TEXT NOT NULL DEFAULT 'joe',
  created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS public.game_worlds (
  id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  name          TEXT NOT NULL,
  seed          BIGINT NOT NULL,
  created_by    UUID NOT NULL REFERENCES public.players(id),
  settings      JSONB NOT NULL DEFAULT '{}',
  build_version TEXT NOT NULL,
  created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS public.game_sessions (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  world_id        UUID NOT NULL REFERENCES public.game_worlds(id),
  host_player_id  UUID NOT NULL REFERENCES public.players(id),
  status          TEXT NOT NULL DEFAULT 'lobby'
                  CHECK (status IN ('lobby', 'active', 'ended')),
  max_players     INT NOT NULL DEFAULT 4,
  current_players INT NOT NULL DEFAULT 0,
  connection_info JSONB NOT NULL DEFAULT '{}',
  build_version   TEXT NOT NULL,
  updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS public.session_players (
  session_id  UUID NOT NULL REFERENCES public.game_sessions(id) ON DELETE CASCADE,
  player_id   UUID NOT NULL REFERENCES public.players(id),
  joined_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  status      TEXT NOT NULL DEFAULT 'connected'
              CHECK (status IN ('connected', 'disconnected')),
  PRIMARY KEY (session_id, player_id)
);

CREATE TABLE IF NOT EXISTS public.world_state (
  world_id    UUID NOT NULL REFERENCES public.game_worlds(id) ON DELETE CASCADE,
  chunk_key   TEXT NOT NULL,
  data        JSONB NOT NULL DEFAULT '{}',
  updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  PRIMARY KEY (world_id, chunk_key)
);

CREATE TABLE IF NOT EXISTS public.player_saves (
  player_id   UUID NOT NULL REFERENCES public.players(id) ON DELETE CASCADE,
  world_id    UUID NOT NULL REFERENCES public.game_worlds(id) ON DELETE CASCADE,
  data        JSONB NOT NULL DEFAULT '{}',
  updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  PRIMARY KEY (player_id, world_id)
);

CREATE INDEX IF NOT EXISTS idx_game_sessions_status_build
  ON public.game_sessions(status, build_version)
  WHERE status = 'lobby';

CREATE INDEX IF NOT EXISTS idx_world_state_world_id
  ON public.world_state(world_id);
