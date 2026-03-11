SELECT 'CREATE DATABASE Goredb' WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = 'Goredb')\gexec

CREATE SCHEMA IF NOT EXISTS userservice;

-- 3. Create the uuidv7 function
-- This is a standard PL/pgSQL implementation so the DEFAULT uuidv7() actually works.
CREATE OR REPLACE FUNCTION uuidv7() RETURNS uuid AS $$
DECLARE
    unix_ts_ms bytea;
    uuid_bytes bytea;
BEGIN
    unix_ts_ms := decode(lpad(to_hex(floor(extract(epoch from clock_timestamp()) * 1000)::bigint), 12, '0'), 'hex');
    uuid_bytes := unix_ts_ms || gen_random_bytes(10);
    uuid_bytes := set_byte(uuid_bytes, 6, (get_byte(uuid_bytes, 6) & 15) | 112); -- bits 12-15 of time_hi_and_version to 0111
    uuid_bytes := set_byte(uuid_bytes, 8, (get_byte(uuid_bytes, 8) & 63) | 128); -- bits 6-7 of clock_seq_hi_and_reserved to 10
    RETURN encode(uuid_bytes, 'hex')::uuid;
END
$$ LANGUAGE plpgsql VOLATILE;

CREATE TABLE IF NOT EXISTS userservice.users
(
    id uuid NOT NULL DEFAULT uuidv7(), 
    username character varying(255),
    password character varying(255),
    role character varying(50),
    CONSTRAINT users_pkey PRIMARY KEY (id)
);

ALTER TABLE userservice.users OWNER TO postgres;

CREATE TABLE IF NOT EXISTS userservice.scans
(
    id uuid NOT NULL DEFAULT uuidv7(), 
    user_id uuid NOT NULL, 
    mountain_id integer NOT NULL,
    "timestamp" timestamp without time zone NOT NULL DEFAULT now(), 

    CONSTRAINT scans_pkey PRIMARY KEY (id),

    -- Foreign Key Relationship
    CONSTRAINT fk_scans_user FOREIGN KEY (user_id) 
        REFERENCES userservice.users (id) 
        ON DELETE CASCADE
);

ALTER TABLE userservice.scans OWNER TO postgres;