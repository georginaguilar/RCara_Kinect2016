SELECT clave_unica,date_format(fecha,'%H:%i'), HOUR(fecha), MINUTE(fecha), count(distinct No_Usuarios )
FROM  extradata_capturev9_videos v
#WHERE engage = "Yes" AND 
#clave_unica IN ( '53A02C94-D461-4DB7-8F9A-2EFD81EAC8CE' , '568FC4A3-FB58-4C12-A2E2-A2E229994BFA', 'Yes', '0.2131'
# )
group by clave_unica,date_format(fecha,'%H:%i'), HOUR(fecha),MINUTE(fecha)