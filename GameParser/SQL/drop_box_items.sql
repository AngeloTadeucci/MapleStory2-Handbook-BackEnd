DROP TABLE IF EXISTS {databaseName}.drop_box_items;

CREATE TABLE {databaseName}.drop_box_items
(
    drop_box_id     int     not null,
    group_id        int     not null,
    item_id         int     not null,
    item_id2        int     not null default 0,
    min_count       int     not null default 1,
    max_count       int     not null default 1,
    weight          int     not null default 0,
    rarity          tinyint not null default 1,
    smart_drop_rate int     not null default 0,
    enchant_level   int     not null default 0,
    INDEX idx_drop_box_items_drop_box_id (drop_box_id),
    INDEX idx_drop_box_items_item_id (item_id),
    INDEX idx_drop_box_items_item_id2 (item_id2)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci;
