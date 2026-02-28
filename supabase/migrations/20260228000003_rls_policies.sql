-- Row Level Security for all tables
ALTER TABLE public.players         ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.game_worlds     ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.game_sessions   ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.session_players ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.world_state     ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.player_saves    ENABLE ROW LEVEL SECURITY;

CREATE POLICY "players_read_all"   ON public.players FOR SELECT USING (true);
CREATE POLICY "players_write_own"  ON public.players FOR UPDATE USING (auth.uid() = id);

CREATE POLICY "worlds_read_all"    ON public.game_worlds FOR SELECT USING (true);
CREATE POLICY "worlds_insert_auth" ON public.game_worlds FOR INSERT WITH CHECK (auth.uid() = created_by);
CREATE POLICY "worlds_update_own"  ON public.game_worlds FOR UPDATE USING (auth.uid() = created_by);

CREATE POLICY "sessions_read_lobby"  ON public.game_sessions FOR SELECT USING (status = 'lobby' OR auth.uid() = host_player_id);
CREATE POLICY "sessions_insert_auth" ON public.game_sessions FOR INSERT WITH CHECK (auth.uid() = host_player_id);
CREATE POLICY "sessions_update_host" ON public.game_sessions FOR UPDATE USING (auth.uid() = host_player_id);

CREATE POLICY "session_players_read" ON public.session_players FOR SELECT
  USING (
    EXISTS (SELECT 1 FROM public.session_players sp WHERE sp.session_id = session_players.session_id AND sp.player_id = auth.uid())
    OR EXISTS (SELECT 1 FROM public.game_sessions gs WHERE gs.id = session_players.session_id AND gs.status = 'lobby')
  );
CREATE POLICY "session_players_write_own" ON public.session_players FOR ALL USING (auth.uid() = player_id);

CREATE POLICY "world_state_session_access" ON public.world_state FOR ALL USING (
  EXISTS (
    SELECT 1 FROM public.game_sessions gs
    JOIN public.session_players sp ON sp.session_id = gs.id
    WHERE gs.world_id = world_state.world_id
      AND gs.status = 'active'
      AND sp.player_id = auth.uid()
      AND sp.status = 'connected'
  )
);

CREATE POLICY "player_saves_own" ON public.player_saves FOR ALL USING (auth.uid() = player_id);
