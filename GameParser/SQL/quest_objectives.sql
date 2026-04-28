DROP TABLE IF EXISTS {databaseName}.quest_objectives;

CREATE TABLE {databaseName}.quest_objectives (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    quest_id INT NOT NULL,
    sequence INT NOT NULL,
    condition_type VARCHAR(64) NOT NULL,
    required_value BIGINT NOT NULL,
    codes JSON NOT NULL,
    targets JSON NOT NULL,
    first_code INT GENERATED ALWAYS AS (
        CASE
            WHEN JSON_UNQUOTE(JSON_EXTRACT(codes, '$[0]')) REGEXP '^[0-9]+$'
            THEN CAST(JSON_UNQUOTE(JSON_EXTRACT(codes, '$[0]')) AS UNSIGNED)
            ELSE NULL
        END
    ) STORED,
    party_count INT NULL,
    guild_party_count INT NULL,
    INDEX idx_quest_objectives_quest_id (quest_id),
    INDEX idx_quest_objectives_condition_type (condition_type),
    INDEX idx_quest_objectives_first_code (first_code),
    INDEX idx_quest_objectives_type_first_code (condition_type, first_code)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci;
