import pyodbc
import os
from pathlib import Path
import logging

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("DBMigrate")

def run_sql_scripts():
    server = 'localhost'
    database = 'rag-pipeline'
    connection_string = f'DRIVER={{ODBC Driver 17 for SQL Server}};SERVER={server};DATABASE={database};Trusted_Connection=yes;'
    
    sql_dir = Path(__file__).resolve().parent.parent.parent.parent / "sql"
    # Using the unified recreation script
    script_name = "recreate_schema.sql"
    
    try:
        conn = pyodbc.connect(connection_string)
        cursor = conn.cursor()
        
        script_path = sql_dir / script_name
        if not script_path.exists():
            logger.error(f"Script {script_name} not found!")
            return
            
        logger.info(f"Executing {script_name}...")
        with open(script_path, 'r') as f:
            sql = f.read()
            # Simple SQL script without GO statements
            try:
                cursor.execute(sql)
                conn.commit()
                logger.info(f"Successfully executed {script_name}")
            except Exception as e:
                logger.error(f"Error executing {script_name}: {e}")
        
        cursor.close()
        conn.close()
    except Exception as e:
        logger.error(f"Migration failed: {e}")

if __name__ == "__main__":
    run_sql_scripts()
