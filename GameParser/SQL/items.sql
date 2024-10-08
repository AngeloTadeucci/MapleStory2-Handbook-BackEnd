DROP TABLE IF EXISTS {databaseName}.items;

CREATE TABLE {databaseName}.items
(
    id                  INT              NOT NULL,
    name                varchar(100)     not NULL,
    tooltip_description text             not NULL,
    guide_description   text             not NULL,
    main_description    text             not NULL,
    rarity              tinyint unsigned not NULL default 1,
    is_outfit           tinyint          not NULL,
    job_limit           JSON             not NULL,
    job_recommend       JSON             not NULL,
    level_min           INT              not NULL default 0,
    level_max           INT              not NULL default 0,
    gender              tinyint unsigned not NULL default 0,
    icon_path           varchar(100)     not null default '',
    visit_count         int              not null default 0,
    pet_id              int              not null default 0,
    is_ugc              tinyint          not null default 0,
    transfer_type       int              not null default 0,
    sellable            tinyint          not null default 0,
    breakable           tinyint          not null default 0,
    fusionable          tinyint          not null default 0,
    skill_id            int              not null default 0,
    skill_level         int              not null default 0,
    stack_limit         int              not null default 0,
    tradeable_count     int              not null default 0,
    repackage_count     int              not null default 0,
    sell_price          JSON             not null,
    kfms                JSON             not null,
    icon_code           tinyint          not null,
    move_disable        tinyint          not null,
    remake_disable      tinyint          not null,
    gear_score          int              not null,
    enchantable         tinyint          not null,
    dyeable             tinyint          not null,
    constants_stats     JSON             NOT NULL,
    static_stats        JSON             NOT NULL,
    random_stats        JSON             NOT NULL,
    random_stat_count   tinyint          not null,
    slot                tinyint unsigned NOT NULL,
    set_info            JSON             NOT NULL,
    set_name            varchar(100)     not null default '',
    item_preset         varchar(4)       not null default '',
    glamour_count       int              not null default 0,
    repackage_scrolls   text             not null,
    repackage_limit     int              not null default 0,
    box_id              int              not null default 0,
    item_type           int              not null default 0,
    represent_option    int              not null default 0,
    additional_effects  text             not null,
    story_book_id       int              not null default 0,
    CONSTRAINT items_pk PRIMARY KEY (id)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci;
