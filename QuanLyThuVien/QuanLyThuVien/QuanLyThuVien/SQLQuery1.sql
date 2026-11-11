INSERT INTO Accounts (Username, Password, Role, IsActive)
VALUES (N'admin', N'Admin@123', N'Admin', 1)

SELECT COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Readers';