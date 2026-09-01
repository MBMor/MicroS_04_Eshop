--
-- PostgreSQL database dump
--

\restrict A6K4fnjaW9CIQ8M1hKmCsRYnMB6zTPkSFcWiRlVbfehh5pPrpeqZILcAkweieYt

-- Dumped from database version 18.3 (Debian 18.3-1.pgdg13+1)
-- Dumped by pg_dump version 18.3 (Debian 18.3-1.pgdg13+1)

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- Data for Name: products; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.products (id, name, sku, description, category, price_amount, currency, is_active, created_at_utc, updated_at_utc) VALUES ('95ec1db7-2bb0-4f59-ab73-06e8e7a13591', 'Mechanical Keyboard', 'KEYBOARD-001', 'Compact mechanical keyboard for testing the catalog flow.', 'Accessories', 2499.00, 'CZK', true, '2026-07-14 08:55:46.903923+00', NULL);
INSERT INTO public.products (id, name, sku, description, category, price_amount, currency, is_active, created_at_utc, updated_at_utc) VALUES ('b9b70474-34bf-41f2-a4a9-da5f73774e20', 'Versioned Product', 'VERSIONED-001', 'Product created through API version 1.', 'Testing', 999.00, 'CZK', true, '2026-07-16 08:43:08.099438+00', NULL);


--
-- PostgreSQL database dump complete
--

\unrestrict A6K4fnjaW9CIQ8M1hKmCsRYnMB6zTPkSFcWiRlVbfehh5pPrpeqZILcAkweieYt

