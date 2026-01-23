DROP TABLE IF EXISTS {databaseName}.map_npcs;

CREATE TABLE {databaseName}.map_npcs (
    uid INT AUTO_INCREMENT PRIMARY KEY,
    map_id INT NOT NULL,
    npc_id INT NOT NULL,
    coord_x INT NOT NULL,
    coord_y INT NOT NULL,
    coord_z INT NOT NULL,
    rotation_x INT NOT NULL,
    rotation_y INT NOT NULL,
    rotation_z INT NOT NULL,
    model_name VARCHAR(200),
    instance_name VARCHAR(200),
    is_spawn_on_field_create TINYINT,
    is_day_die TINYINT,
    is_night_die TINYINT,
    patrol_data_uuid VARCHAR(100),
    INDEX idx_map_npcs_map_id (map_id),
    INDEX idx_map_npcs_npc_id (npc_id)
);
