DROP TABLE IF EXISTS {databaseName}.quest_rewards;

CREATE TABLE {databaseName}.quest_rewards (
    id BIGINT AUTO_INCREMENT PRIMARY KEY,
    quest_id INT NOT NULL,
    reward_kind ENUM('start', 'complete') NOT NULL,
    item_id INT NOT NULL,
    count INT NOT NULL,
    reward_rank INT NOT NULL,
    INDEX idx_quest_rewards_quest_id (quest_id),
    INDEX idx_quest_rewards_item_id (item_id),
    INDEX idx_quest_rewards_item_kind (item_id, reward_kind)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci;
