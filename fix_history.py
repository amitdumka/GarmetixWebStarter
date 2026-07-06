import psycopg2
try:
    conn = psycopg2.connect('host=localhost dbname=garmetix_db user=postgres password=postgres')
    cur = conn.cursor()
    cur.execute("DELETE FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '20260705171502_CreateSaaSTables'")
    conn.commit()
    print('Deleted migration history row.')
except Exception as e:
    print(e)
