-- Nach Cluster-Erstellung als admin (IAM-Token) ausführen.
-- Aurora DSQL: feste Passwörter gibt es nicht; Rolle + IAM GRANT.

-- Anwendungsrolle (gewünschter Name "verwaltung")
CREATE ROLE verwaltung WITH LOGIN;

-- Ersetzen: ACCOUNT_ID und ProjectName aus dem Stack
-- AWS IAM GRANT verwaltung TO 'arn:aws:iam::ACCOUNT_ID:role/taetigkeitsbericht-ec2-role';

-- Datenbank-Name: in DSQL ggf. eingeschränkt – falls CREATE DATABASE nicht geht,
-- Schema/Tabellen in der Standard-DB anlegen und Connection auf diese DB richten.
-- CREATE DATABASE "Taetigkeitsbericht";

GRANT CONNECT ON DATABASE postgres TO verwaltung;
-- Nach Schema-Anlage:
-- GRANT USAGE ON SCHEMA public TO verwaltung;
-- GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO verwaltung;
