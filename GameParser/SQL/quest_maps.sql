DROP TABLE IF EXISTS {databaseName}.quest_maps;

CREATE TABLE {databaseName}.quest_maps (
    uid INT AUTO_INCREMENT PRIMARY KEY,
    quest_id INT NOT NULL,
    map_id INT NOT NULL,
    UNIQUE INDEX idx_quest_map_unique (quest_id, map_id),
    INDEX idx_quest_maps_quest_id (quest_id),
    INDEX idx_quest_maps_map_id (map_id)
);
