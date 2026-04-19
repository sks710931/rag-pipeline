import pyodbc
import logging

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("DBInit")

def create_database():
    server = 'localhost'
    database = 'master'
    # Using ODBC Driver 17 as detected
    connection_string = f'DRIVER={{ODBC Driver 17 for SQL Server}};SERVER={server};DATABASE={database};Trusted_Connection=yes;'
    
    try:
        # autocommit=True is required for CREATE DATABASE statements
        conn = pyodbc.connect(connection_string, autocommit=True)
        cursor = conn.cursor()
        
        # Check if DB exists
        cursor.execute("SELECT name FROM sys.databases WHERE name = 'rag-pipeline'")
        if cursor.fetchone():
            logger.info("Database 'rag-pipeline' already exists.")
        else:
            logger.info("Creating database 'rag-pipeline'...")
            cursor.execute("CREATE DATABASE [rag-pipeline]")
            logger.info("Database 'rag-pipeline' created successfully.")
            
        cursor.close()
        conn.close()
    except Exception as e:
        logger.error(f"Failed to create database: {e}")

if __name__ == "__main__":
    create_database()
