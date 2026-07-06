import psycopg2
try:
    conn = psycopg2.connect('host=localhost dbname=garmetix_db user=postgres password=postgres')
    cur = conn.cursor()
    cur.execute("SELECT table_name FROM information_schema.tables WHERE table_schema='public'")
    tables = [r[0] for r in cur.fetchall()]
    print('TenantSubscriptions in DB:', 'TenantSubscriptions' in tables)
    print('SaaSClients in DB:', 'SaaSClients' in tables)
except Exception as e:
    print(e)
