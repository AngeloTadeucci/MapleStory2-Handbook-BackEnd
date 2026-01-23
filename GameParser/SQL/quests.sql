DROP TABLE IF EXISTS {databaseName}.quests;

create table {databaseName}.quests (
    id int primary key,
    name varchar(255) not null,
    description text not null,
    manualDescription text not null,
    completeDescription text not null,
    questType int not null,
    questLevel int not null,
    requiredLevel int not null,
    requiredQuest JSON not null,
    selectableQuest JSON not null,
    startNpcId int not null,
    completeNpcId int not null,
    startRewards JSON not null,
    completeRewards JSON not null,
    visit_count int not null default 0
) engine = InnoDB default charset = utf8mb4 collate = utf8mb4_0900_ai_ci;