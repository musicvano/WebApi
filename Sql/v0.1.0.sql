DROP TABLE IF EXISTS packages;

CREATE TABLE packages
(
    id          uuid PRIMARY KEY,
    name        character varying        NOT NULL UNIQUE,
    description character varying        NOT NULL,
    repository  character varying        NOT NULL UNIQUE,
    license     character varying        NOT NULL,
    created     timestamp with time zone NOT NULL
);
