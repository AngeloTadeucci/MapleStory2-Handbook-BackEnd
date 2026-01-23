DROP TABLE IF EXISTS {databaseName}.map_mobs;

CREATE TABLE {databaseName}.map_mobs (
    uid INT AUTO_INCREMENT PRIMARY KEY,
    map_id INT NOT NULL,
    spawn_point_id INT NOT NULL,
    npc_id INT NOT NULL,
    coord_x INT NOT NULL,
    coord_y INT NOT NULL,
    coord_z INT NOT NULL,
    rotation_x INT NOT NULL,
    rotation_y INT NOT NULL,
    rotation_z INT NOT NULL,
    min_difficulty INT DEFAULT 0,
    max_difficulty INT DEFAULT 0,
    population INT DEFAULT 1,
    cooldown INT DEFAULT 0,
    pet_population INT DEFAULT 0,
    pet_spawn_rate INT DEFAULT 0,
    INDEX idx_map_mobs_map_id (map_id),
    INDEX idx_map_mobs_npc_id (npc_id),
    INDEX idx_map_mobs_spawn_point (spawn_point_id)
);
