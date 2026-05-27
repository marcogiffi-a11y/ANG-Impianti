-- ANG-Impianti v3.0 — Tabelle Mary (libreria + memoria apprendimento)
-- Eseguire UNA volta in Supabase SQL Editor del progetto fezkgexyvbduuurodggz

-- 1) Layer elettrici ANG_*
create table if not exists mary_layer_elettrici (
  id uuid default gen_random_uuid() primary key,
  nome text unique not null,                  -- Es: 'ANG_PRESE_FM'
  descrizione text,
  colore_aci smallint default 7,              -- ACI 1-255 AutoCAD
  colore_rgb text,                            -- Per anteprime UI
  tipo_linea text default 'Continuous',
  spessore_mm numeric(4,2) default 0.25,
  stato_default text default 'visibile',
  created_at timestamptz default now()
);

-- 2) Simboli elettrici (la legenda di Marco)
create table if not exists mary_simboli (
  id uuid default gen_random_uuid() primary key,
  nome text not null,                         -- 'PT bipasso 10/16A'
  categoria text not null,                    -- Prese / Luci / Interruttori / ...
  geometria jsonb not null,                   -- {entities:[...], bbox_w, bbox_h, count}
  layer_nome text default 'ANG_GENERICO',
  bbox_w_cm numeric(8,2),
  bbox_h_cm numeric(8,2),
  num_entities int,
  png_anteprima text,                         -- URL Storage (opzionale)
  created_at timestamptz default now()
);

-- 3) Regole di posizionamento simboli (FASE 2)
create table if not exists mary_regole_posizionamento (
  id uuid default gen_random_uuid() primary key,
  simbolo_id uuid references mary_simboli(id) on delete cascade,
  contesto_vano text,                         -- 'cucina', 'soggiorno', '*'
  distanza_muro_cm numeric(6,2),
  altezza_da_pavimento_cm numeric(6,2),
  rotazione_gradi numeric(6,2),
  regola_testuale text,                       -- 'PT bipasso a 30cm dal muro, parallela alla parete'
  created_at timestamptz default now()
);

-- 4) Oggetti riconosciuti (mobili, sanitari, arredi)
create table if not exists mary_oggetti_riconosciuti (
  id uuid default gen_random_uuid() primary key,
  nome text not null,                         -- 'Tavolo da pranzo'
  categoria text not null,                    -- Arredo / Sanitario / Elettrodomestico / ...
  geometria jsonb not null,
  bbox_w_cm numeric(8,2),
  bbox_h_cm numeric(8,2),
  num_entities int,
  regola_elettrica jsonb,                     -- {tipo_presa, n_prese, altezza, distanza}
  conferme_count int default 1,
  created_at timestamptz default now()
);

-- 5) Memoria progetti (stile e pattern di disegno)
create table if not exists mary_esperienza_progetti (
  id uuid default gen_random_uuid() primary key,
  nome_file text,
  tipo_immobile text,
  mq_totali numeric(10,2),
  conteggi_per_layer jsonb,                   -- {ANG_PRESE_FM: 14, ANG_LUCI: 8, ...}
  totale_entita_ang int,
  note text,
  created_at timestamptz default now()
);

-- 6) Regole aggregate apprese (deduzioni automatiche)
create table if not exists mary_regole_apprese (
  id uuid default gen_random_uuid() primary key,
  regola_testo text not null,                 -- 'Cucina 10-15mq → 6-8 PT'
  contesto text,
  evidenza_count int default 1,
  confidenza_perc numeric(5,2) default 50,
  created_at timestamptz default now()
);

-- RLS
alter table mary_layer_elettrici enable row level security;
alter table mary_simboli enable row level security;
alter table mary_regole_posizionamento enable row level security;
alter table mary_oggetti_riconosciuti enable row level security;
alter table mary_esperienza_progetti enable row level security;
alter table mary_regole_apprese enable row level security;

create policy "Anon read mary" on mary_layer_elettrici for select using (true);
create policy "Anon write mary" on mary_layer_elettrici for all using (true);
create policy "Anon read sim" on mary_simboli for select using (true);
create policy "Anon write sim" on mary_simboli for all using (true);
create policy "Anon all rpos" on mary_regole_posizionamento for all using (true);
create policy "Anon all ogg" on mary_oggetti_riconosciuti for all using (true);
create policy "Anon all esp" on mary_esperienza_progetti for all using (true);
create policy "Anon all reg" on mary_regole_apprese for all using (true);

-- Indici
create index if not exists idx_simboli_cat on mary_simboli(categoria, nome);
create index if not exists idx_ogg_cat on mary_oggetti_riconosciuti(categoria);
create index if not exists idx_esp_data on mary_esperienza_progetti(created_at desc);

-- LAYER DI DEFAULT (popola la libreria con i layer ANG standard)
insert into mary_layer_elettrici (nome, descrizione, colore_aci, spessore_mm) values
  ('ANG_PRESE_FM',     'Prese forza motrice',                3, 0.25),
  ('ANG_LUCI',         'Punti luce e corpi illuminanti',    2, 0.25),
  ('ANG_COMANDI',      'Interruttori, deviatori, pulsanti', 6, 0.25),
  ('ANG_CIRC_LUCI',    'Linee circuito illuminazione',      1, 0.35),
  ('ANG_CIRC_PRESE',   'Linee circuito prese',              3, 0.35),
  ('ANG_QUADRI',       'Quadri elettrici',                   5, 0.50),
  ('ANG_VANI',         'Polilinee vani / locali',           4, 0.13),
  ('ANG_ETICHETTE',    'Numeri circuito ed etichette',      7, 0.18),
  ('ANG_GENERICO',     'Generico ANG',                       7, 0.25)
on conflict (nome) do nothing;
