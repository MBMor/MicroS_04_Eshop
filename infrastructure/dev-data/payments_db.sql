--
-- PostgreSQL database dump
--

\restrict jP9aqYL86cGrHJUNy4fG8oOObn7eztZXTybRlW20T5bmiSD6NYddQRvZek47TeE

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
-- Data for Name: outbox_messages; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.outbox_messages (id, event_id, event_type, routing_key, payload, occurred_at_utc, correlation_id, trace_parent, trace_state, status, retry_count, last_error, published_at_utc, claimed_at_utc, claimed_by, next_attempt_at_utc) VALUES ('bfca9dec-b935-4816-abf4-d426c09d41e0', '20ee4165-541c-437b-98b4-224e3c5c5412', 'Eshop.Contracts.IntegrationEvents.V1.PaymentAuthorizedV1', 'payment.authorized.v1', '{"amount": 999.00, "eventId": "20ee4165-541c-437b-98b4-224e3c5c5412", "orderId": "422207f9-0c7c-4077-8580-35dc70282638", "currency": "CZK", "paymentId": "e2b8b00d-1aa7-40ff-9dcc-95c257c9a79b", "customerId": "local-development-user", "correlationId": "9d66e840-258d-48f8-8348-ceb2334a09c6", "occurredAtUtc": "2026-07-17T10:07:35.1199209+00:00"}', '2026-07-17 10:07:35.11992+00', '9d66e840-258d-48f8-8348-ceb2334a09c6', NULL, NULL, 'Published', 0, NULL, '2026-07-17 10:07:36.325892+00', NULL, NULL, NULL);
INSERT INTO public.outbox_messages (id, event_id, event_type, routing_key, payload, occurred_at_utc, correlation_id, trace_parent, trace_state, status, retry_count, last_error, published_at_utc, claimed_at_utc, claimed_by, next_attempt_at_utc) VALUES ('130c65dd-5058-43ae-8856-89f7337ed407', 'dc7d0974-77d7-4206-b497-f1604d24f5ae', 'Eshop.Contracts.IntegrationEvents.V1.PaymentFailedV1', 'payment.failed.v1', '{"amount": 999.00, "reason": "Simulated payment failure.", "eventId": "dc7d0974-77d7-4206-b497-f1604d24f5ae", "orderId": "dbf626e8-9f55-4d5a-8530-4096d8dd039c", "currency": "CZK", "paymentId": "ecf725fb-763d-4dc0-bfd6-0c9a7e0958ae", "customerId": "local-development-user", "correlationId": "dc41aa42-3241-4e10-bcbf-5550e4114996", "occurredAtUtc": "2026-07-17T10:14:35.6469806+00:00"}', '2026-07-17 10:14:35.64698+00', 'dc41aa42-3241-4e10-bcbf-5550e4114996', NULL, NULL, 'Published', 0, NULL, '2026-07-17 10:14:36.753489+00', NULL, NULL, NULL);
INSERT INTO public.outbox_messages (id, event_id, event_type, routing_key, payload, occurred_at_utc, correlation_id, trace_parent, trace_state, status, retry_count, last_error, published_at_utc, claimed_at_utc, claimed_by, next_attempt_at_utc) VALUES ('24e0b1f1-0eb5-418b-8cfb-6555c06ad114', 'e5397c10-f283-4ace-946f-b1e2328a8c94', 'Eshop.Contracts.IntegrationEvents.V1.PaymentAuthorizedV1', 'payment.authorized.v1', '{"amount": 3498.00, "eventId": "e5397c10-f283-4ace-946f-b1e2328a8c94", "orderId": "852569d3-a505-41a6-8872-2ba7b3efe424", "currency": "CZK", "paymentId": "810253ff-9044-458d-9be6-a2889664ce59", "customerId": "984ef162-6832-438f-800d-7972989000fb", "correlationId": "73406ecc-6323-4e4b-ba1c-369e55a85e85", "occurredAtUtc": "2026-07-22T12:11:26.6678021+00:00"}', '2026-07-22 12:11:26.667802+00', '73406ecc-6323-4e4b-ba1c-369e55a85e85', '00-7171f9378d54f7e53fada9f54d9d2971-d6350684ada87e16-01', NULL, 'Published', 0, NULL, '2026-07-22 12:11:28.987311+00', NULL, NULL, NULL);
INSERT INTO public.outbox_messages (id, event_id, event_type, routing_key, payload, occurred_at_utc, correlation_id, trace_parent, trace_state, status, retry_count, last_error, published_at_utc, claimed_at_utc, claimed_by, next_attempt_at_utc) VALUES ('5c5ab284-6ad4-47af-95fb-6ce05642e32e', '517109dd-034e-4885-89c6-9f16b9826d93', 'Eshop.Contracts.IntegrationEvents.V1.PaymentAuthorizedV1', 'payment.authorized.v1', '{"amount": 2499.00, "eventId": "517109dd-034e-4885-89c6-9f16b9826d93", "orderId": "0d51de0d-7219-4a99-9939-7644b131e2ec", "currency": "CZK", "paymentId": "358e648f-497a-4ec1-a238-046290ba3835", "customerId": "984ef162-6832-438f-800d-7972989000fb", "correlationId": "44afe649-9741-45d6-8805-546b80d97160", "occurredAtUtc": "2026-08-31T10:47:39.7429526+00:00"}', '2026-08-31 10:47:39.742952+00', '44afe649-9741-45d6-8805-546b80d97160', '00-ab923a93f0974478a7f4e0dc6ce2306b-3e8a918fcb6b3539-01', NULL, 'Published', 0, NULL, '2026-08-31 10:47:42.264835+00', NULL, NULL, NULL);
INSERT INTO public.outbox_messages (id, event_id, event_type, routing_key, payload, occurred_at_utc, correlation_id, trace_parent, trace_state, status, retry_count, last_error, published_at_utc, claimed_at_utc, claimed_by, next_attempt_at_utc) VALUES ('b478f62c-2d13-4858-8824-fde85912d591', '235419de-b93c-4573-a5f2-243dba0e78b3', 'Eshop.Contracts.IntegrationEvents.V1.PaymentAuthorizedV1', 'payment.authorized.v1', '{"amount": 999.00, "eventId": "235419de-b93c-4573-a5f2-243dba0e78b3", "orderId": "2f0cbe03-d3df-40b4-a32c-39bf703d6d28", "currency": "CZK", "paymentId": "d48e44bf-50ec-4f60-ba08-db7eb5e08999", "customerId": "984ef162-6832-438f-800d-7972989000fb", "correlationId": "531e89d3-e6e9-493f-a3de-7975fb609456", "occurredAtUtc": "2026-08-31T11:12:46.5779198+00:00"}', '2026-08-31 11:12:46.577919+00', '531e89d3-e6e9-493f-a3de-7975fb609456', '00-ac2cc133a8353772779bcaaf3e5fedcc-38bf1e0d51a08520-01', NULL, 'Published', 0, NULL, '2026-08-31 11:12:47.628914+00', NULL, NULL, NULL);
INSERT INTO public.outbox_messages (id, event_id, event_type, routing_key, payload, occurred_at_utc, correlation_id, trace_parent, trace_state, status, retry_count, last_error, published_at_utc, claimed_at_utc, claimed_by, next_attempt_at_utc) VALUES ('ae33e777-c569-45f7-bad8-474f675d34c3', '272bef44-9329-4d0a-a62f-aa5c1182daaa', 'Eshop.Contracts.IntegrationEvents.V1.PaymentAuthorizedV1', 'payment.authorized.v1', '{"amount": 2499.00, "eventId": "272bef44-9329-4d0a-a62f-aa5c1182daaa", "orderId": "5ec8fdc3-5cd3-4308-a5c6-125433ff7c5b", "currency": "CZK", "paymentId": "c2020e2e-dab1-4096-aacf-235e543d0848", "customerId": "984ef162-6832-438f-800d-7972989000fb", "correlationId": "67157742-092d-476f-a8b2-8c540ec49d55", "occurredAtUtc": "2026-08-31T11:34:20.1415214+00:00"}', '2026-08-31 11:34:20.141521+00', '67157742-092d-476f-a8b2-8c540ec49d55', '00-acf09ccff4be909fa165d1ca669436ba-1a8663503b12ae47-01', NULL, 'Published', 0, NULL, '2026-08-31 11:34:21.564431+00', NULL, NULL, NULL);
INSERT INTO public.outbox_messages (id, event_id, event_type, routing_key, payload, occurred_at_utc, correlation_id, trace_parent, trace_state, status, retry_count, last_error, published_at_utc, claimed_at_utc, claimed_by, next_attempt_at_utc) VALUES ('5f0feb08-0ed3-413f-ade1-c3d5226357c4', '3d7e20a4-7636-4ad1-84f4-56b1a6de0309', 'Eshop.Contracts.IntegrationEvents.V1.PaymentAuthorizedV1', 'payment.authorized.v1', '{"amount": 2499.00, "eventId": "3d7e20a4-7636-4ad1-84f4-56b1a6de0309", "orderId": "c6e3ac3c-f324-4d8d-898b-9e73a1f0481d", "currency": "CZK", "paymentId": "8f01c606-8727-404f-8883-6d26dabed71c", "customerId": "984ef162-6832-438f-800d-7972989000fb", "correlationId": "89e974af-cbea-4f6d-bb6a-1851dd50b699", "occurredAtUtc": "2026-08-31T13:26:52.613385+00:00"}', '2026-08-31 13:26:52.613385+00', '89e974af-cbea-4f6d-bb6a-1851dd50b699', '00-0d76dd0020485768302269a6fdb6be6a-5675cff089ba723d-01', NULL, 'Published', 0, NULL, '2026-08-31 13:26:54.053284+00', NULL, NULL, NULL);


--
-- Data for Name: payments; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.payments (id, order_id, customer_id, amount, currency, payment_method, status, failure_reason, created_at_utc, processed_at_utc) VALUES ('48f66eed-0104-484b-a917-25ec990821b6', 'a9854edd-e548-431b-9ded-980e8014d6e7', 'local-development-user', 2499.00, 'CZK', 'test-success', 'Authorized', NULL, '2026-07-16 07:16:07.216152+00', '2026-07-16 07:16:07.216152+00');
INSERT INTO public.payments (id, order_id, customer_id, amount, currency, payment_method, status, failure_reason, created_at_utc, processed_at_utc) VALUES ('f1c6b4ff-4030-45c1-8a23-2c81e478575c', 'c59bcb98-5640-4888-97c3-270c14b64190', 'local-development-user', 1299.00, 'CZK', 'test-fail', 'Failed', 'Simulated payment failure.', '2026-07-16 07:16:59.151116+00', '2026-07-16 07:16:59.151116+00');
INSERT INTO public.payments (id, order_id, customer_id, amount, currency, payment_method, status, failure_reason, created_at_utc, processed_at_utc) VALUES ('e2b8b00d-1aa7-40ff-9dcc-95c257c9a79b', '422207f9-0c7c-4077-8580-35dc70282638', 'local-development-user', 999.00, 'CZK', 'test-success', 'Authorized', NULL, '2026-07-17 10:07:35.11992+00', '2026-07-17 10:07:35.11992+00');
INSERT INTO public.payments (id, order_id, customer_id, amount, currency, payment_method, status, failure_reason, created_at_utc, processed_at_utc) VALUES ('ecf725fb-763d-4dc0-bfd6-0c9a7e0958ae', 'dbf626e8-9f55-4d5a-8530-4096d8dd039c', 'local-development-user', 999.00, 'CZK', 'test-fail', 'Failed', 'Simulated payment failure.', '2026-07-17 10:14:35.64698+00', '2026-07-17 10:14:35.64698+00');
INSERT INTO public.payments (id, order_id, customer_id, amount, currency, payment_method, status, failure_reason, created_at_utc, processed_at_utc) VALUES ('810253ff-9044-458d-9be6-a2889664ce59', '852569d3-a505-41a6-8872-2ba7b3efe424', '984ef162-6832-438f-800d-7972989000fb', 3498.00, 'CZK', 'test-success', 'Authorized', NULL, '2026-07-22 12:11:26.667802+00', '2026-07-22 12:11:26.667802+00');
INSERT INTO public.payments (id, order_id, customer_id, amount, currency, payment_method, status, failure_reason, created_at_utc, processed_at_utc) VALUES ('358e648f-497a-4ec1-a238-046290ba3835', '0d51de0d-7219-4a99-9939-7644b131e2ec', '984ef162-6832-438f-800d-7972989000fb', 2499.00, 'CZK', 'test-success', 'Authorized', NULL, '2026-08-31 10:47:39.742952+00', '2026-08-31 10:47:39.742952+00');
INSERT INTO public.payments (id, order_id, customer_id, amount, currency, payment_method, status, failure_reason, created_at_utc, processed_at_utc) VALUES ('d48e44bf-50ec-4f60-ba08-db7eb5e08999', '2f0cbe03-d3df-40b4-a32c-39bf703d6d28', '984ef162-6832-438f-800d-7972989000fb', 999.00, 'CZK', 'test-success', 'Authorized', NULL, '2026-08-31 11:12:46.577919+00', '2026-08-31 11:12:46.577919+00');
INSERT INTO public.payments (id, order_id, customer_id, amount, currency, payment_method, status, failure_reason, created_at_utc, processed_at_utc) VALUES ('c2020e2e-dab1-4096-aacf-235e543d0848', '5ec8fdc3-5cd3-4308-a5c6-125433ff7c5b', '984ef162-6832-438f-800d-7972989000fb', 2499.00, 'CZK', 'test-success', 'Authorized', NULL, '2026-08-31 11:34:20.141521+00', '2026-08-31 11:34:20.141521+00');
INSERT INTO public.payments (id, order_id, customer_id, amount, currency, payment_method, status, failure_reason, created_at_utc, processed_at_utc) VALUES ('8f01c606-8727-404f-8883-6d26dabed71c', 'c6e3ac3c-f324-4d8d-898b-9e73a1f0481d', '984ef162-6832-438f-800d-7972989000fb', 2499.00, 'CZK', 'test-success', 'Authorized', NULL, '2026-08-31 13:26:52.613385+00', '2026-08-31 13:26:52.613385+00');


--
-- Data for Name: processed_messages; Type: TABLE DATA; Schema: public; Owner: -
--

INSERT INTO public.processed_messages (event_id, consumer_name, processed_at_utc) VALUES ('9ee594d2-7c95-4278-89af-643a219c7663', 'payments.payment-requested.v1', '2026-07-22 12:11:26.667802+00');
INSERT INTO public.processed_messages (event_id, consumer_name, processed_at_utc) VALUES ('04ab888e-634c-4720-a7d1-8a5d19f856a7', 'payments.payment-requested.v1', '2026-08-31 10:47:39.742952+00');
INSERT INTO public.processed_messages (event_id, consumer_name, processed_at_utc) VALUES ('96d59c1f-d2a4-4d10-992e-aaf293a7f874', 'payments.payment-requested.v1', '2026-08-31 11:12:46.577919+00');
INSERT INTO public.processed_messages (event_id, consumer_name, processed_at_utc) VALUES ('d45a731d-5c49-4cf9-bd8c-7d6d36646f2e', 'payments.payment-requested.v1', '2026-08-31 11:34:20.141521+00');
INSERT INTO public.processed_messages (event_id, consumer_name, processed_at_utc) VALUES ('5935ea89-10a0-4b95-889a-dbf30ab0640d', 'payments.payment-requested.v1', '2026-08-31 13:26:52.613385+00');


--
-- PostgreSQL database dump complete
--

\unrestrict jP9aqYL86cGrHJUNy4fG8oOObn7eztZXTybRlW20T5bmiSD6NYddQRvZek47TeE

