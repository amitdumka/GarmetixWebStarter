import urllib.request
import json
import psycopg2

req = urllib.request.Request('http://localhost:5000/api/saas/clients')
try:
    urllib.request.urlopen(req)
except Exception as e:
    op_id = json.loads(e.read().decode())['operationId']
    
    conn = psycopg2.connect("host=127.0.0.1 dbname=garmetix user=garmetix password=garmetix_dev")
    cur = conn.cursor()
    cur.execute('SELECT "Message", "DetailsJson" FROM "ApplicationMessageLogs" WHERE "OperationId" = %s', (op_id,))
    row = cur.fetchone()
    print("ERROR:", row[0])
    print("DETAILS:", row[1])
