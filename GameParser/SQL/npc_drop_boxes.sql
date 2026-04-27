DROP TABLE IF EXISTS {databaseName}.npc_drop_boxes;

CREATE TABLE {databaseName}.npc_drop_boxes
(
    npc_id      int     not null,
    drop_box_id int     not null,
    drop_type   tinyint not null default 0,
    INDEX idx_npc_drop_boxes_npc_id (npc_id),
    INDEX idx_npc_drop_boxes_drop_box_id (drop_box_id),
    INDEX idx_npc_drop_boxes_npc_drop_type (npc_id, drop_type)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci;
