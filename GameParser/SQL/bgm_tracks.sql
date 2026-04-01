DROP TABLE IF EXISTS {databaseName}.bgm_tracks;

create table {databaseName}.bgm_tracks (
  id int not null auto_increment,
  name varchar(200) not null,
  file_name varchar(200) not null,
  source_bank varchar(100) not null default '',
  frequency int not null default 44100,
  channels int not null default 2,
  duration_seconds decimal(8,2) not null default 0,
  loop_start int not null default 0,
  loop_end int not null default 0,
  event_id int not null default 0,
  visit_count int not null default 0,
  constraint bgm_tracks_pk primary key (id),
  index idx_bgm_tracks_event_id (event_id)
) engine = InnoDB default charset = utf8mb4 collate = utf8mb4_0900_ai_ci;
