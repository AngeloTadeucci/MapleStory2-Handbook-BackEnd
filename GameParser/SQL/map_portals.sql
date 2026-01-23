DROP TABLE IF EXISTS {databaseName}.map_portals;

CREATE TABLE {databaseName}.map_portals (
    uid INT AUTO_INCREMENT PRIMARY KEY,
    map_id INT NOT NULL,
    portal_id INT NOT NULL,
    name VARCHAR(200),
    destination_map_id INT NOT NULL,
    target_portal_id INT NOT NULL,
    coord_x INT NOT NULL,
    coord_y INT NOT NULL,
    coord_z INT NOT NULL,
    rotation_x INT NOT NULL,
    rotation_y INT NOT NULL,
    rotation_z INT NOT NULL,
    portal_type INT NOT NULL,
    is_enabled TINYINT,
    is_visible TINYINT,
    minimap_visible TINYINT,
    trigger_id INT,
    INDEX idx_map_portals_map_id (map_id),
    INDEX idx_map_portals_destination (destination_map_id)
);
