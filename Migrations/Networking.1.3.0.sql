CREATE OR REPLACE FUNCTION triggerservice_gettriggersbysensorid(id VARCHAR(24))
    RETURNS TABLE(
		"TriggerID" BIGINT,
		"ActionID" BIGINT,
		"SensorID" VARCHAR(24),
		"KeyValue" VARCHAR(32),
		"LowerEdge" NUMERIC,
		"UpperEdge" NUMERIC,
		"FormalLanguage" TEXT,
		"Type" INTEGER,
		"Channel" INTEGER,
		"Target" VARCHAR(255),
		"Message" TEXT
    )
    LANGUAGE plpgsql
AS
$$
BEGIN
	RETURN QUERY
	SELECT 
		DISTINCT ON (ta."ID")
		t."ID" AS "TriggerID",
		ta."ID" AS "ActionID",
		t."SensorID",
		t."KeyValue",
		t."LowerEdge",
		t."UpperEdge",
		t."FormalLanguage",
		t."Type",
		ta."Channel",
		ta."Target",
		ta."Message"
	FROM "TriggerActions" AS ta
	INNER JOIN "Triggers" AS t ON t."ID" = ta."TriggerID"
	WHERE t."SensorID" = id
	ORDER BY ta."ID";
END;
$$;

CREATE OR REPLACE FUNCTION triggerservice_gettriggers()
    RETURNS TABLE(
		"TriggerID" BIGINT,
		"ActionID" BIGINT,
		"SensorID" VARCHAR(24),
		"KeyValue" VARCHAR(32),
		"LowerEdge" NUMERIC,
		"UpperEdge" NUMERIC,
		"FormalLanguage" TEXT,
		"Type" INTEGER,
		"Channel" INTEGER,
		"Target" VARCHAR(255),
		"Message" TEXT
    )
    LANGUAGE plpgsql
AS
$$
BEGIN
	RETURN QUERY
	SELECT 
		DISTINCT ON (ta."ID")
		t."ID" AS "TriggerID",
		ta."ID" AS "ActionID",
		t."SensorID",
		t."KeyValue",
		t."LowerEdge",
		t."UpperEdge",
		t."FormalLanguage",
		t."Type",
		ta."Channel",
		ta."Target",
		ta."Message"
	FROM "TriggerActions" AS ta
	INNER JOIN "Triggers" AS t ON t."ID" = ta."TriggerID"
	ORDER BY ta."ID";
END;
$$;


GRANT EXECUTE ON FUNCTION triggerservice_gettriggers() TO db_triggerservice;
GRANT EXECUTE ON FUNCTION triggerservice_gettriggersbysensorid(id VARCHAR(24)) TO db_triggerservice;

DROP FUNCTION triggerservice_createinvocation(triggerid BIGINT, actionid BIGINT, timestmp TIMESTAMP);
DROP FUNCTION router_gettriggersbyid(id varchar(24));
DROP FUNCTION public.admin_truncatetriggerinvocations();
DROP FUNCTION router_gettriggersbyid(id varchar(24));
DROP FUNCTION dataapi_selectinvocationcount(idlist TEXT);
DROP TABLE "TriggerInvocations";
