-- Replace placeholders before running.
-- This simulates one external app writing a Customer master-data event into Oracle.

MERGE INTO GARMETIX_SYNC_EVENTS target
USING (SELECT '__EVENT_ID__' AS EVENT_ID FROM DUAL) source
ON (target.EVENT_ID = source.EVENT_ID)
WHEN MATCHED THEN UPDATE SET
    PAYLOAD_JSON = '{"tenantId":"__TENANT_ID__","sourceApplication":"ExternalAppSmokeTest","entityName":"Customer","entityId":"__CUSTOMER_ID__","operation":"Upsert","versionUtc":"2026-06-08T00:00:00Z","payload":{"id":"__CUSTOMER_ID__","companyId":"__COMPANY_ID__","name":"Oracle External Customer","mobileNumber":"9999999999","address":"External App Test Address","city":"OracleCloud","state":"Jharkhand","country":"India","zipCode":"814101","registred":true,"synced":true,"deleted":false}}',
    PAYLOAD_HASH = STANDARD_HASH('{"id":"__CUSTOMER_ID__"}', 'SHA256'),
    VERSION_UTC = SYS_EXTRACT_UTC(SYSTIMESTAMP),
    SOURCE_APPLICATION = 'ExternalAppSmokeTest',
    APPLY_STATUS = 'Pending',
    APPLY_ERROR = NULL,
    APPLIED_UTC = NULL
WHEN NOT MATCHED THEN INSERT (
    EVENT_ID, TENANT_ID, SOURCE_APPLICATION, ENTITY_NAME, ENTITY_ID, OPERATION,
    VERSION_UTC, PAYLOAD_HASH, PAYLOAD_JSON, CREATED_UTC, APPLY_STATUS
) VALUES (
    '__EVENT_ID__', '__TENANT_ID__', 'ExternalAppSmokeTest', 'Customer', '__CUSTOMER_ID__', 'Upsert',
    SYS_EXTRACT_UTC(SYSTIMESTAMP), STANDARD_HASH('{"id":"__CUSTOMER_ID__"}', 'SHA256'),
    '{"tenantId":"__TENANT_ID__","sourceApplication":"ExternalAppSmokeTest","entityName":"Customer","entityId":"__CUSTOMER_ID__","operation":"Upsert","versionUtc":"2026-06-08T00:00:00Z","payload":{"id":"__CUSTOMER_ID__","companyId":"__COMPANY_ID__","name":"Oracle External Customer","mobileNumber":"9999999999","address":"External App Test Address","city":"OracleCloud","state":"Jharkhand","country":"India","zipCode":"814101","registred":true,"synced":true,"deleted":false}}',
    SYS_EXTRACT_UTC(SYSTIMESTAMP), 'Pending'
);

COMMIT;
