DROP TABLE IF EXISTS {databaseName}.furnishing_shop;

CREATE TABLE {databaseName}.furnishing_shop
(
    item_id    int     not null,
    buyable    tinyint not null default 0,
    token_type tinyint not null default 0,
    price      int     not null default 0,
    CONSTRAINT furnishing_shop_pk PRIMARY KEY (item_id),
    INDEX idx_furnishing_shop_token_type (token_type),
    INDEX idx_furnishing_shop_buyable (buyable)
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_0900_ai_ci;
