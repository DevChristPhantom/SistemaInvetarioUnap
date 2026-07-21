# Informe de Paridad de Comportamiento — SisLab-Topo (Java → C#/.NET)

**Fecha:** 2026-07-20
**Fase:** 8 — QA final, revisión de seguridad, informe de paridad
**Alcance:** comparación de comportamiento entre `sislab-topo` (Java/Swing, referencia
de solo lectura) y `sislab-topo-net` (C#/.NET 10, WPF), tras Fases 0-7.

---

## Resumen ejecutivo (para el dueño del proyecto)

La migración a C#/.NET está terminada y pasó por una revisión final completa: se probó
la aplicación real de principio a fin (no solo pruebas automatizadas), se revisó la
seguridad del código, y se corrigió un error real que apareció durante esa prueba.

**¿Qué se hizo en esta última fase?**
Se recorrió la aplicación entera como la usaría el personal del laboratorio: primer
arranque, creación de la contraseña de administrador, inicio de sesión, el panel
principal con sus gráficos, registrar un equipo nuevo, registrar un préstamo de 6
equipos a la vez (el máximo permitido), generar e imprimir el comprobante en PDF,
registrar la devolución, exportar e importar el inventario en Excel, cambiar la
contraseña, cerrar sesión y volver a entrar. También se probó a propósito "romper" la
aplicación: intentar prestar un equipo que ya estaba prestado, intentar borrar un
equipo prestado, provocar el bloqueo por contraseña incorrecta y verificar que
sobrevive incluso si se cierra y se vuelve a abrir el programa, y usar el código de
recuperación para resetear la contraseña sin perder ningún dato.

**¿Qué se encontró y se corrigió?**
Durante ese recorrido apareció un error real (no cubierto por las 162 pruebas
automatizadas existentes): al cerrar sesión, la aplicación se cerraba de golpe con un
error técnico, en vez de volver a la pantalla de acceso. La causa era un detalle de
"fontanería" interna (cómo se reutilizan las ventanas al navegar entre ellas) que solo
se manifestaba la segunda vez que alguien cerraba sesión dentro de la misma ejecución
del programa — por eso ninguna prueba automática ni verificación visual anterior lo
había detectado, ya que ninguna fase anterior había cerrado sesión dos veces seguidas
en el mismo proceso. Se corrigió, se volvió a probar el flujo completo (incluyendo
cerrar sesión dos veces seguidas) y quedó funcionando con normalidad. Este es
exactamente el tipo de hallazgo que justifica un proceso de QA manual además de las
pruebas automatizadas.

**¿Por qué se puede confiar más en esta versión que en la anterior?**
La causa raíz original de las "fallas de sistema" reportadas — el archivo Excel usado
como base de datos, que se corrompía si el programa se cerraba a mitad de una
escritura — ya no existe: ahora los datos viven en un archivo SQLite con transacciones
reales (o se guarda todo el cambio, o no se guarda nada; nunca queda a medias). La
contraseña de administrador ya no viene de fábrica ni se anuncia en el instalador; el
propio sistema obliga a crear una la primera vez que se usa. El bloqueo por intentos
fallidos, que antes se olvidaba con solo reiniciar el programa, ahora se recuerda de
verdad. Y se añadió un mecanismo de recuperación de contraseña con un código de un solo
uso, para no quedar nunca sin acceso. Todo lo demás — la apariencia, el flujo de
trabajo, las reglas de préstamos y el formato exacto del comprobante en PDF — se
mantuvo igual a propósito, para que el cambio de sistema no obligue a reaprender nada.

---

## 1. Reglas de negocio y comportamientos preservados 1:1 respecto a Java

| Regla / comportamiento | Cómo se verificó |
|---|---|
| No se puede prestar un equipo que ya está en préstamo activo | UI real: al abrir "Buscar Equipo Disponible" para un nuevo préstamo, los 6 equipos ya prestados no aparecen en la lista (solo los 2 restantes). Test automatizado: `PrestamoServiceTests`. |
| No se puede eliminar un equipo en préstamo activo | UI real: al intentar eliminar "EQ-QA-001" (en préstamo), aparece el error "No se puede eliminar el equipo porque se encuentra en un préstamo activo." Test automatizado: `EquipoServiceTests`. |
| No se puede registrar una segunda devolución sobre el mismo préstamo | Verificado por diseño de la UI (un préstamo devuelto desaparece de "Préstamos Activos" y ya no hay ningún botón que permita re-devolverlo) + cobertura explícita en `PrestamoServiceTests` (puerto del test Java original de esta regla). |
| Límite de 6 equipos por préstamo | UI real: el formulario de "Nuevo Préstamo" solo tiene 6 filas de equipo (con "Buscar"/"Quitar"); no existe forma de agregar una 7ª. Se completó un préstamo real con exactamente 6 equipos distintos y se guardó con éxito. |
| Bloqueo de 3 intentos fallidos → 30s, rechaza incluso la contraseña correcta | UI real, con control de tiempo exacto: se provocó el bloqueo, se cerró el proceso por completo y se volvió a abrir 2.6s después — el mensaje mostró "Cuenta bloqueada por 27 segundos" y la contraseña correcta fue rechazada; pasados los 30s, la misma contraseña fue aceptada con normalidad. Persistido en la tabla `AppState` de SQLite (antes vivía solo en memoria en Java). |
| Reglas de disponibilidad / rollback de préstamo | Servicio (`PrestamoService`) usa una transacción real de EF Core: si falla cualquier parte del guardado, se revierte todo (ver log "Revirtiendo transacción..."). Cubierto en `PrestamoServiceTests`. |
| Agregados del Dashboard (relleno de ceros de los últimos 6 meses, Top-5 más prestados) | Verificado visualmente en el Dashboard real tras registrar un préstamo: el gráfico de barras mensual y la tabla "Equipos Más Prestados (Top 5)" reflejan los datos reales. Cobertura en `DashboardViewModelTests`/`PrestamoServiceTests`. |
| Formato exacto del comprobante PDF (encabezado, grid de solicitante, tabla de 7 filas, firmas) | Verificación automática de contenido/estructura (`SisLabTopo.Reports.Tests`) + comparación visual manual contra un PDF generado por la versión Java (hecha en Fase 3) + nueva verificación visual de la vista previa en esta fase (préstamo real de 6 equipos). |
| Costo de hash de contraseña BCrypt = 12 | Igual que la versión Java (`AuthService`, constante `CostoBCrypt`). |
| Mensajes de validación en español | Copiados literalmente de las anotaciones Java (`@NotBlank`, `@Size(max=200)`, etc.) a FluentValidation. |
| El código de recuperación permite restablecer la contraseña sin perder datos | UI real: tras usar el código de recuperación del primer arranque para fijar una nueva contraseña, se confirmó que los 8 equipos y el préstamo devuelto en Historial seguían intactos. |
| Import/Export de Excel como función de interoperabilidad | UI real: se exportó el inventario (8 equipos) a `.xlsx` y se importó un Excel de prueba con 7 equipos nuevos (columnas Código/Denominación/Modelo/Marca/Serie/Estado/Tipo/Observación), con `StatusBadge` reflejando correctamente estados "Bueno"/"Regular". |

## 2. Mejoras deliberadas introducidas durante la migración

| Mejora | Justificación |
|---|---|
| Motor de almacenamiento SQLite (EF Core) en vez de reescritura completa del `.xlsx` | Elimina la causa raíz confirmada de la corrupción de datos reportada ("fallas de sistema"): ya no hay una ventana en la que un cierre inesperado deje el archivo de datos a medio escribir. |
| Transacciones reales en préstamo/devolución | Si algo falla a mitad de un préstamo con varios equipos, se revierte todo el conjunto — antes (Java) una falla parcial podía dejar equipos marcados como no disponibles sin un préstamo asociado. |
| Asistente de primer arranque + código de recuperación, en vez de contraseña `admin123` hardcodeada | La contraseña por defecto Java quedaba además anunciada en texto plano por el instalador — un hueco de seguridad real, ya cerrado. El código de recuperación de un solo uso evita quedar sin acceso sin reintroducir una puerta trasera fija. |
| Bloqueo de intentos fallidos persistido en base de datos | En Java, reiniciar el proceso reseteaba el contador de intentos fallidos — un bypass trivial del bloqueo. Ahora sobrevive a un reinicio del proceso (verificado explícitamente en esta fase). |
| UI async con indicadores de carga | Corrige el bloqueo de la interfaz que tenía la versión Swing durante operaciones de E/S (login, carga de listas, generación de PDF, Excel). |
| `StatusBadge` consistente en toda la app | Un único control reutilizable para "Disponible/No disponible", "Bueno/Regular/Malo", "Devuelto/Activo", etc., en vez de estilos ad-hoc repetidos por pantalla como en Swing. |
| Corrección del problema N+1 en las tablas | Las consultas de listado (equipos, préstamos, historial) traen los datos relacionados en una sola consulta en vez de una consulta adicional por fila. |
| Sistema de diseño centralizado (`Themes/Colors.xaml`, `Typography.xaml`, `Spacing.xaml`) realmente aplicado | Corrige inconsistencias de espaciado/color que existían en la versión Swing (estilos definidos por pantalla, no centralizados). |
| DatePicker real en Historial | La versión Java filtraba por fecha con campos de texto libre; ahora son selectores de calendario reales (visto en la captura de Historial de esta fase). |
| Botón "Copiar base de datos ahora" (Configuración) | Con un solo archivo SQLite, respaldar es copiar un archivo — más simple y más seguro que el `.xlsx` que podía quedar corrupto a medio escribir. |
| Log de advertencia (Serilog) para valores de estado/tipo no reconocidos al importar | En Java ese caso caía en silencio al valor por defecto; ahora queda una traza para poder auditar datos de origen inconsistente. |

## 3. Limitaciones y pendientes conocidos (honestos)

- **La prueba de regresión de `LoginViewRenderingTests.cs` no protege contra el bug
  específico de "ventana en blanco" por tier de renderizado.** Se comprobó
  empíricamente que, corriendo dentro del proceso host de `dotnet test`, tanto
  `RenderTargetBitmap` como `PrintWindow` producen contenido visual correcto incluso
  con la línea `RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly` comentada en
  `App.xaml.cs` — el proceso de pruebas no reproduce las condiciones de tier de
  hardware que sí afectaron al ejecutable empaquetado real el 2026-07-20. La prueba sí
  protege contra regresiones de plantilla/binding que dejen la ventana sin contenido,
  pero no contra una futura eliminación accidental de esa línea. Documentado
  explícitamente en el propio archivo de test como pendiente para cuando exista un
  pipeline de CI que corra sobre una máquina con las mismas condiciones de hardware/
  virtualización que esta estación de desarrollo.
- **El contador "Equipos Disponibles" de la barra de estado del Shell solo se refresca
  al navegar entre pantallas**, no inmediatamente tras registrar/devolver un préstamo o
  eliminar un equipo desde la misma pantalla (`ShellViewModel.ActualizarBarraEstadoAsync`
  se invoca únicamamente desde `Navegar<T>()` y desde el evento `Loaded` de `ShellView`).
  Se observó directamente en esta fase: tras guardar un préstamo de 6 equipos sin
  cambiar de pantalla, la barra siguió mostrando el valor anterior hasta navegar a otra
  sección. No afecta la exactitud de los datos (las pantallas de Equipos/Préstamos sí
  se refrescan solas), solo a ese contador puntual.
- **LiveCharts2 (`LiveChartsCore.SkiaSharpView.WPF`) es una librería pre-1.0** (versión
  2.0.5, la última estable publicada; la serie 2.1.x solo tiene versiones `-dev`
  prerelease). Se usa para el donut de estado de equipos y el gráfico de barras
  mensual del Dashboard, ambos verificados visualmente en esta fase con datos reales.
  Riesgo aceptado: es la librería de gráficos WPF de código abierto más madura
  disponible para .NET 10 hoy; no se identificó una alternativa estable superior.
- **Warning `NU1701`** (`OpenTK`, `OpenTK.GLWpfControl`, `SkiaSharp.Views.WPF`,
  dependencias transitivas de LiveCharts2, resueltas contra `.NETFramework` en vez de
  `net10.0-windows7.0`): se investigó si existe una versión más nueva de
  `SkiaSharp.Views.WPF` dirigida a .NET moderno (existe, 4.150.1) pero está fijada por
  la propia declaración de dependencias de `LiveChartsCore.SkiaSharpView.WPF` 2.0.5 (la
  última versión estable de ese paquete), no por este proyecto; forzarla de forma
  independiente arriesgaría una incompatibilidad con LiveCharts2 sin beneficio real,
  dado que **en la práctica la librería funciona correctamente pese al warning** (donut
  y barras del Dashboard se ven y actualizan bien, confirmado visualmente en esta
  fase). Se documenta la decisión de dejarlo así hasta que LiveCharts2 publique una
  versión estable con dependencias actualizadas.
- **Warning `NU1510`** (`System.Drawing.Common` en `SisLabTopo.UI.Tests`, "no se
  eliminará, es probable que no sea necesario"): paquete usado únicamente por
  `LoginViewRenderingTests.cs` (comparación de píxeles capturados por `PrintWindow`).
  Es una referencia directa deliberada del proyecto de test, no un rastro accidental;
  se deja el warning documentado en vez de suprimirlo, ya que quitar el paquete
  rompería esa prueba.
- **`SQLitePCLRaw.lib.e_sqlite3` (NU1903 / CVE-2025-6965): resuelto en esta fase.** Ver
  sección de seguridad más abajo para el detalle completo.
- El bug real encontrado y corregido en esta misma fase (crash al cerrar sesión, ver
  más abajo) es un recordatorio honesto de que ninguna fase anterior había ejercitado
  el ciclo completo "iniciar sesión → cerrar sesión → iniciar sesión" dos veces
  seguidas dentro del mismo proceso — el recorrido de Fase 5 se detenía en un solo
  ciclo. Se corrigió y quedó cubierto por el recorrido manual completo de esta fase,
  pero no existe todavía una prueba automatizada que ejercite específicamente "cerrar
  sesión dos veces en el mismo proceso" — queda como sugerencia de prueba futura.

## 4. Bug real encontrado y corregido en esta fase (Fase 8)

**Síntoma:** al hacer clic en "Cerrar sesión" por segunda vez dentro de la misma
ejecución del programa (Login → Shell → Cerrar sesión → Login → **Shell → Cerrar
sesión**), la aplicación terminaba abruptamente con una excepción no controlada,
confirmada en el Visor de Eventos de Windows:

```
System.InvalidOperationException: No se puede establecer Visibility ni llamar a Show,
ShowDialog o WindowInteropHelper.EnsureHandle después de haberse cerrado un elemento
Window.
   en NavigationService.ShowRootWindow[TWindow](Object viewModel)
   en ShellViewModel.CerrarSesion()
```

**Causa raíz:** `LoginView` y `ShellView` (junto con `LoginViewModel`/`ShellViewModel`)
estaban registrados como `Scoped` en el contenedor de DI. Como toda la aplicación
resuelve sus servicios desde un único `IServiceScope` de por vida (ver
`App.OnStartup`), un registro `Scoped` se comporta en la práctica como un *singleton de
sesión*: el contenedor devuelve siempre la misma instancia de `LoginView`. La primera
transición Login→Shell cierra esa instancia de `LoginView` (`previousWindow?.Close()`
en `NavigationService.ShowRootWindow`) sin problema, porque todavía no se había vuelto
a pedir. Pero un `Window` de WPF, una vez cerrado, no puede volver a mostrarse — es una
restricción del framework, no un bug de esta app. La segunda vez que se navega de
vuelta a `LoginViewModel` (segundo "Cerrar sesión"), el contenedor devuelve esa misma
instancia ya cerrada, y `Window.Show()` lanza la excepción de arriba.

**Corrección aplicada:** `LoginView`, `ShellView`, `LoginViewModel` y `ShellViewModel`
pasaron de `AddScoped` a `AddTransient` en
`src/SisLabTopo.UI/Startup/ServiceRegistration.cs` — mismo patrón que ya usaban
`FirstRunSetupView`/`FirstRunSetupViewModel`. Cada navegación a Login o Shell crea
ahora una ventana y un ViewModel nuevos (sus dependencias siguen siendo Scoped/
Singleton del scope raíz de la app, así que no hay ningún costo funcional). Se
verificó cerrando sesión dos veces seguidas tras la corrección: la app vuelve
limpiamente a la pantalla de acceso las dos veces, sin excepciones.

**Por qué no se detectó antes:** ninguna prueba automatizada (162 tests) ejercita el
ciclo de vida completo de `App`/`NavigationService` con un contenedor de DI real —
las pruebas de ViewModel usan mocks de `INavigationService`. El recorrido manual de la
Fase 5 verificó login→Shell una sola vez por sesión de prueba. Solo apareció al
recorrer explícitamente "cerrar sesión → volver a iniciar sesión" como pide el
checklist de esta Fase 8.

---

## 5. Revisión de seguridad (Fase 8)

- **Sin rastros de `admin123` ni contraseñas hardcodeadas.** Barrido completo
  (`grep`) de todo `src/` y `tests/`: las únicas coincidencias son comentarios que
  documentan explícitamente que ese valor por defecto **ya no existe** (en
  `AuthService.cs`, `App.xaml.cs`, `FirstRunSetupViewModel.cs`,
  `ConfiguracionViewModel.cs`, `AuthServiceTests.cs`). Ningún hash bcrypt embebido como
  valor por defecto en el código.
- **`Startup/DevPasswordSeeder.cs` (parche temporal de la Fase 4) confirmado
  eliminado**: no existe el archivo, y no queda ninguna referencia a la clase en el
  código (solo comentarios que documentan históricamente su eliminación).
- **Serilog no registra contraseñas ni códigos de recuperación en texto plano.**
  Revisadas todas las llamadas a `_logger`/`Log` en `src/` (más de 50 sitios): ninguna
  interpola la variable de contraseña o de código de recuperación; los mensajes cerca
  del manejo de contraseñas (`AuthService.CrearContrasenaInicialAsync`,
  `RestablecerContrasenaConCodigoAsync`) solo describen el evento ("contraseña de
  administrador configurada y código de recuperación generado"), nunca el valor.
  Además, las variables `char[]` de contraseña/código se limpian explícitamente con
  `Array.Clear` en bloques `finally`.
- **Guard de path-traversal en `EquipoService.ImportarDesdeExcelAsync` confirmado
  vigente**: exige ruta absoluta (`Path.IsPathFullyQualified`) y rechaza cualquier ruta
  que contenga `".."`, igual que la versión Java (`File.isAbsolute()` +
  `!path.contains("..")`). Verificado además funcionalmente en esta fase: la
  importación real de un Excel de prueba (ruta absoluta legítima) funcionó con
  normalidad.
- **`NU1903` (`SQLitePCLRaw.lib.e_sqlite3` 2.1.11, CVE-2025-6965, severidad alta):
  resuelto.** El advisory de GitHub no declara una "versión corregida" formal para el
  ecosistema NuGet (`first_patched_version: null`), pero sí existe una versión más
  nueva de esa dependencia transitiva (2.1.12, dentro de la misma serie 2.x — la serie
  3.x es un salto mayor que reempaqueta SQLite de forma potencialmente incompatible con
  `Microsoft.Data.Sqlite` 10.0.10 y se descartó por riesgo). Se fijó explícitamente
  `SQLitePCLRaw.bundle_e_sqlite3` a la versión `2.1.12` con una referencia directa en
  `src/SisLabTopo.Data/SisLabTopo.Data.csproj` (ver comentario en el propio archivo).
  `Microsoft.EntityFrameworkCore.Sqlite` ya estaba en su última versión estable
  disponible (10.0.10; la única versión más nueva es `11.0.0-preview.*`, descartada por
  no ser estable). Tras el cambio: `dotnet restore`/`build` ya no reportan `NU1903`, y
  los 162 tests se volvieron a ejecutar en verde sin ningún cambio de comportamiento.
- **Otros warnings de build revisados**: ver sección de limitaciones (`NU1701`,
  `NU1510`) arriba — ambos documentados y sin corrección disponible mejor que la
  actual.

---

## 6. Verificación final

- `dotnet build`: **0 errores**, **14 warnings** (antes ~30; bajó tras resolver
  `NU1903` para los 5 proyectos que lo reportaban — de los ~16 warnings restantes,
  todos son `NU1701`/`NU1510` ya documentados y justificados arriba).
- `dotnet test` (desde la raíz del repo): **162 de 162 pruebas en verde**
  (`SisLabTopo.Data.Tests`: 24, `SisLabTopo.Reports.Tests`: 13,
  `SisLabTopo.Services.Tests`: 43, `SisLabTopo.UI.Tests`: 82) — mismo total que antes
  de esta fase; no se agregaron ni se quitaron pruebas, se corrigió un bug de
  producción (DI lifetime) que las pruebas existentes no cubrían y se confirmó que la
  corrección no rompió ninguna.
- Sistema dejado limpio: sin la aplicación instalada de forma permanente (solo se
  ejecutó el build de Debug directamente, nunca el instalador de esta fase); datos de
  desarrollo de `%APPDATA%\SisLabTopo`/`%LOCALAPPDATA%\SisLabTopo` respaldados antes de
  forzar el primer arranque y restaurados byte a byte al finalizar (verificado con
  `diff`); sin procesos `SisLabTopo.UI.exe` en ejecución al terminar.
