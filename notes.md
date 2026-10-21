# RÃ©ponses aux questions d'analyse du fichier `docker-compose.yml`

## Q1. Le fichier dÃ©clare quatre services alors que le TP d'aujourd'hui n'en utilise qu'un. Pourquoi, Ã  votre avis ?

- **ContinuitÃ© pÃ©dagogique et fil conducteur :** Le projet sert de fil conducteur pour plusieurs sÃ©ances de TP abordant l'architecture de **persistance polyglotte** (*Polyglot Persistence*). Les commentaires du fichier le confirment :
  - **PostgreSQL** : donnÃ©es transactionnelles (sÃ©ance 1)
  - **MongoDB** : catalogue de jeux (sÃ©ance 2)
  - **Redis** : classements, matchmaking, cache (sÃ©ance 3)
  - **Neo4j** : graphe social et recommandation (sÃ©ance 4)
- **SimplicitÃ© de configuration :** DÃ©finir dÃ¨s le dÃ©part l'ensemble de la stack Ã©vite d'avoir Ã  modifier la configuration Docker Ã  chaque sÃ©ance. Les Ã©tudiants disposent ainsi immÃ©diatement de l'environnement complet et cohÃ©rent pour l'ensemble du module.

---

## Q2. Trois services ont une section volumes, un seul n'en a pas. Lequel, et qu'est-ce que Ã§a implique pour ses donnÃ©es ?

- **Le service concernÃ© :** Il s'agit de **Redis** (`pixelhub-redis`).
- **Implications pour ses donnÃ©es :**
  - Sans volume montÃ© sur l'hÃ´te, les donnÃ©es stockÃ©es dans Redis sont **entiÃ¨rement volatiles**.
  - Si le conteneur est supprimÃ© (par exemple avec `docker compose down`), toutes les donnÃ©es en cache ou clÃ©s stockÃ©es sont dÃ©finitivement perdues.
  - Cela reste cohÃ©rent avec son rÃ´le principal (cache, classements Ã©phÃ©mÃ¨res, donnÃ©es de session en mÃ©moire), contrairement aux autres bases (Postgres, Mongo, Neo4j) dont les donnÃ©es doivent persister au-delÃ  du cycle de vie des conteneurs grÃ¢ce Ã  leurs volumes nommÃ©s respectifs (`pg_data`, `mongo_data`, `neo4j_data`).

---

## Q3. Que signifie la ligne `"15432:5432"` ? Les deux nombres ne dÃ©signent pas la mÃªme chose.

La syntaxe Docker pour la redirection de ports est sous la forme `"<port_hÃ´te>:<port_conteneur>"` :
- **`15432` (Port hÃ´te) :** C'est le port ouvert sur votre propre machine (l'hÃ´te). Comme indiquÃ© en tÃªte du fichier compose, ce port est dÃ©calÃ© par rapport au port standard (5432) afin d'Ã©viter tout conflit avec un service PostgreSQL qui tournerait dÃ©jÃ  localement sur la machine physique de l'Ã©tudiant.
- **`5432` (Port conteneur) :** C'est le port d'Ã©coute par dÃ©faut du dÃ©mon PostgreSQL Ã  l'intÃ©rieur du conteneur Docker.

**Signification :** Docker redirige les connexions arrivant sur le port `15432` de votre machine hÃ´te vers le port `5432` du conteneur PostgreSQL.

---

## Q4. Les mots de passe sont Ã©crits en clair dans le fichier. Est-ce acceptable ici ? Le serait-ce sur un serveur de production ?

- **Acceptable ici (en TP / dÃ©veloppement local) :**
  - **Oui**, car il s'agit d'un environnement d'apprentissage local avec des donnÃ©es de test/factices, sans exposition publique sur Internet.
  - Cela simplifie la prise en main par les Ã©tudiants sans nÃ©cessiter la mise en place d'une infrastructure complexe de gestion des secrets.
- **En production :**
  - **Absolument non.** Les fichiers de configuration comme `docker-compose.yml` sont gÃ©nÃ©ralement archivÃ©s sur des dÃ©pÃ´ts de code (ex. Git). Laisser des identifiants en clair expose le systÃ¨me Ã  des fuites de donnÃ©es et Ã  des compromissions directes.
  - En production, on utilise des outils dÃ©diÃ©s Ã  la gestion des secrets (tels que **Docker Secrets**, **Kubernetes Secrets**, **HashiCorp Vault**, ou des variables d'environnement injectÃ©es de faÃ§on sÃ©curisÃ©e via un coffre-fort de secrets ou un pipeline CI/CD).

---

## Q5. Les commandes ci-dessus utilisent `docker compose exec`. Qu'est-ce que Ã§a fait exactement ? OÃ¹ s'exÃ©cute la commande `redis-cli` ?

- **RÃ´le de `docker compose exec` :**
  - Cette commande permet d'exÃ©cuter un processus ou une commande arbitraire **Ã  l'intÃ©rieur d'un conteneur dÃ©jÃ  en cours d'exÃ©cution** (*running*). Docker Compose identifie le conteneur associÃ© au nom du service (ex: `redis`, `postgres`, `mongo`) et transmet la commande au dÃ©mon Docker pour exÃ©cution dans l'environnement isolÃ© de ce conteneur.
- **OÃ¹ s'exÃ©cute `redis-cli` ?**
  - La commande `redis-cli` s'exÃ©cute **Ã  l'intÃ©rieur du conteneur `pixelhub-redis`** (dans son propre systÃ¨me de fichiers et son espace de nommage rÃ©seau/processus), et **non sur la machine hÃ´te**.
  - **Avantage pratique :** Cela Ã©vite d'avoir Ã  installer les outils d'administration ou clients en ligne de commande (`redis-cli`, `mongosh`, `pg_isready`, etc.) sur son systÃ¨me d'exploitation hÃ´te : on exploite directement les binaires embarquÃ©s dans l'image Docker du service.

---

## Q6. La base de donnÃ©es tourne dans un conteneur Docker, et la chaÃ®ne de connexion dit `Host=localhost`. Pourquoi est-ce que Ã§a fonctionne ?

- **MÃ©canisme de redirection de port (Port Binding) :**
  - Dans le fichier `docker-compose.yml`, la section `ports` de PostgreSQL dÃ©clare `"- \"${PG_PORT:-15432}:5432\""`.
  - Docker mappe ainsi le port d'Ã©coute interne `5432` du conteneur sur le port `15432` de l'interface rÃ©seau de la **machine hÃ´te** (`localhost`).
  - Lorsque l'API s'exÃ©cute sur votre machine et tente de se connecter Ã  `Host=localhost;Port=15432`, elle envoie ses requÃªtes au port local de l'hÃ´te, que le dÃ©mon Docker intercepte et achemine fidÃ¨lement vers le port `5432` du conteneur PostgreSQL.

---

## Q7. ArrÃªtez l'application (Ctrl+C), relancez-la. Les trois joueurs sont-ils dupliquÃ©s ? Pourquoi ?

- **Non, les trois joueurs ne sont pas dupliquÃ©s.**
- **Pourquoi ? (Explication dans le code et la persistance) :**
  1. **Condition d'initialisation dans `Program.cs` :**
     L'insertion des trois joueurs est encapsulÃ©e dans une condition :
     ```csharp
     if (!db.Players.Any())
     {
         db.Players.AddRange(...);
         db.SaveChanges();
     }
     ```
     Avant d'insÃ©rer, EF Core exÃ©cute une requÃªte `SELECT EXISTS(...)` sur la table `Players`. DÃ¨s lors que la table contient dÃ©jÃ  des enregistrements, `db.Players.Any()` retourne `true` (donc `!db.Players.Any()` est `false`), et l'insertion initiale est ignorÃ©e.
  2. **Persistance des donnÃ©es via le volume Docker :**
     Comme PostgreSQL utilise un volume nommÃ© (`pg_data`), les lignes insÃ©rÃ©es lors du premier dÃ©marrage restent Ã©crites et persistÃ©es sur le disque mÃªme aprÃ¨s l'arrÃªt ou le redÃ©marrage de l'API.




---
---

# TP 2 â€” Le catalogue de jeux dans MongoDB

## Q1. Vous retrouvez le champ `_id`, dÃ©jÃ  aperÃ§u au TP 1 (Ã©tape 6.2), que personne n'a Ã©crit dans `games.json`. Cette fois, intÃ©ressez-vous Ã  qui l'a fabriquÃ© : le serveur MongoDB, ou l'outil qui a envoyÃ© les documents (`mongoimport`) ? Comparez avec l'`Id` 4 de *Pixel* au TP 1 (Q8) : qui l'avait choisi, et Ã  quel moment le connaissiez-vous ?

- **Qui fabrique l'`_id` dans MongoDB :**
  - C'est **l'outil client** (`mongoimport` ici, ou le driver MongoDB en C#) qui gÃ©nÃ¨re l'`ObjectId` cÃ´tÃ© client avant de transmettre le document au serveur. La structure mÃªme d'un `ObjectId` (4 octets de timestamp, 5 octets alÃ©atoires propres Ã  la machine/processus, et 3 octets de compteur) a Ã©tÃ© pensÃ©e pour Ãªtre gÃ©nÃ©rÃ©e de faÃ§on dÃ©centralisÃ©e sans risque de collision.
- **Comparaison avec l'`Id` 4 de *Pixel* (PostgreSQL / TP 1) :
  - Dans PostgreSQL, la clÃ© primaire Ã©tait gÃ©nÃ©rÃ©e par une sÃ©quence auto-incrÃ©mentÃ©e (`IDENTITY` / `serial`) gÃ©rÃ©e par le **serveur PostgreSQL**.
  - L'identifiant est attribuÃ© uniquement au moment oÃ¹ l'`INSERT` s'exÃ©cute sur le serveur. On ne le connaÃ®t donc qu'**aprÃ¨s** l'Ã©criture en base (via le rÃ©sultat SQL ou aprÃ¨s `SaveChanges()`). Avec MongoDB, l'identifiant peut Ãªtre connu et manipulÃ© cÃ´tÃ© client avant mÃªme l'Ã©criture.

---

## Q2. Ã€ l'exercice 5, `plateformes` est un tableau et vous avez Ã©crit `{ plateformes: "Switch" }`, sans opÃ©rateur particulier. Comment auriez-vous fait la mÃªme chose en SQL, avec un modÃ¨le relationnel normalisÃ© ?

- En SQL normalisÃ© (3NF), on ne peut pas stocker de liste multivaluÃ©e directement dans une colonne atomique (1Ã¨re Forme Normale).
- Il aurait fallu dÃ©composer le modÃ¨le en **trois tables** :
  1. Une table `jeux (id, titre, ...)`
  2. Une table `plateformes (id, nom)`
  3. Une table associative / de jointure `jeux_plateformes (jeu_id, plateforme_id)`
- La requÃªte aurait alors nÃ©cessitÃ© une **jointure** (`JOIN`) :
  ```sql
  SELECT j.titre
  FROM jeux j
  JOIN jeux_plateformes jp ON j.id = jp.jeu_id
  JOIN plateformes p ON jp.plateforme_id = p.id
  WHERE p.nom = 'Switch';
  ```
- **Apport de MongoDB :** Le support natif des tableaux permet d'interroger directement les Ã©lÃ©ments d'une liste sans jointure relationnelle.

---

## Q3. Vous tapez `--authenticationDatabase admin` depuis le TP 1 sans qu'on vous ait dit pourquoi. Pourquoi faut-il l'ajouter aux commandes `mongoimport` et `mongosh` ? OÃ¹ se trouve le compte `pixelhub` ?

- Dans MongoDB, les comptes utilisateurs sont associÃ©s et stockÃ©s dans une base de donnÃ©es d'authentification spÃ©cifique.
- Le compte utilisateur `pixelhub` a Ã©tÃ© crÃ©Ã© dans la base systÃ¨me **`admin`** (lors de l'initialisation du conteneur via `MONGO_INITDB_ROOT_USERNAME` dans `docker-compose.yml`).
- Par dÃ©faut, si l'on ne prÃ©cise pas `--authenticationDatabase`, MongoDB cherche les identifiants dans la base cible Ã  laquelle on se connecte (`pixelhub`), oÃ¹ ce compte n'existe pas, provoquant un Ã©chec d'authentification (`MongoAuthenticationException`).
- L'option `--authenticationDatabase admin` indique au serveur de vÃ©rifier les credentials contre la base `admin`.

---

## Q4. Personne ne vous a arrÃªtÃ©. Est-ce une bonne nouvelle ou un problÃ¨me ? Argumentez en imaginant un projet Ã  quatre dÃ©veloppeurs, six mois plus tard.

- **Une bonne nouvelle Ã  court terme (flexibilitÃ© & prototypage) :**
  - TrÃ¨s grande agilitÃ© : aucune migration DDL bloquante pour ajouter des champs.
  - ReprÃ©sentation naturelle de documents polymorphes ou hÃ©tÃ©rogÃ¨nes (ex: un FPS stocke `joueursParEquipe` et `cartes`, un jeu de gestion stocke `dureeMoyennePartieHeures`).
- **Un problÃ¨me majeur Ã  terme en Ã©quipe :**
  - Dans un projet Ã  quatre dÃ©veloppeurs aprÃ¨s six mois, l'absence de contrainte de schÃ©ma (*schema-less* par dÃ©faut) peut entraÃ®ner de graves dÃ©rives :
    - Fautes de frappe dans les clÃ©s (`nbNiveaux` vs `nombreNiveaux`) non dÃ©tectÃ©es.
    - Types de donnÃ©es incohÃ©rents (chaÃ®ne dans un document, entier dans un autre).
    - Champs orphelins ou donnÃ©es abandonnÃ©es sans documentation.
  - CÃ´tÃ© applicatif C#, la dÃ©sÃ©rialisation Ã©chouera (`FormatException`) ou nÃ©cessitera un surcroÃ®t de code dÃ©fensif.
  - En production, il est donc indispensable de poser des rÃ¨gles de validation de schÃ©ma (ex. *JSON Schema Validation* dans MongoDB) et des conventions d'Ã©quipe.

---

## Q5. Le rÃ©sultat de l'exercice 9 vous surprend-il ? Que s'est-il passÃ© exactement, et pourquoi n'y a-t-il eu aucun message d'erreur ?

- **Ce qui s'est passÃ© :**
  - MongoDB est **sensible Ã  la casse** : la collection `jeux` est diffÃ©rente de `Jeux`.
  - MongoDB utilise une approche de crÃ©ation Ã  la demande (*lazy*) : une lecture `find()` sur une collection inexistante n'est pas considÃ©rÃ©e comme une erreur. MongoDB consulte la collection demandÃ©e, constate qu'elle ne contient aucun document correspondant au filtre, et renvoie un curseur vide (`[]`).
  - Aucun message d'erreur n'est Ã©mis car rechercher dans une collection vide est une opÃ©ration syntaxiquement et logiquement valide pour le moteur.
  - *(Remarque : un `insertOne` sur `db.Jeux` aurait crÃ©Ã© silencieusement une deuxiÃ¨me collection `Jeux` distincte).*

---

## Q6. L'exercice 16 Ã©choue. Recopiez le message d'erreur, et expliquez pourquoi MongoDB refuse. Quelle commande utiliseriez-vous si vous vouliez rÃ©ellement remplacer le document en entier ?

- **Message d'erreur obtenu :**
  ```text
  MongoInvalidArgumentError: Update document requires atomic operators
  ```
- **Pourquoi MongoDB refuse :**
  - La mÃ©thode `updateOne` est dÃ©diÃ©e aux modifications partielles de documents et exige obligatoirement des opÃ©rateurs atomiques (tels que `$set`, `$inc`, `$push`, `$unset`).
  - Sans opÃ©rateur comme `$set`, passer `{ note: 4.4 }` risquerait d'Ã©craser par accident l'intÃ©gralitÃ© du document (en supprimant tous ses autres champs Ã  l'exception de l'`_id`). Le driver bloque donc l'appel par sÃ©curitÃ©.
- **Commande pour rÃ©ellement remplacer le document :**
  - Pour remplacer un document dans sa globalitÃ©, MongoDB impose d'utiliser explicitement la mÃ©thode `replaceOne` :
    ```javascript
    db.jeux.replaceOne({ titre: "Valorant" }, { titre: "Valorant", note: 4.4, genre: "FPS" })
    ```

---

## Q7. Ã€ l'exercice 12, vous avez ajoutÃ© un champ `nbVotes` Ã  un seul document, alors que les dix autres n'en ont pas. Quelle commande SQL aurait Ã©tÃ© nÃ©cessaire pour faire l'Ã©quivalent en relationnel, et quelle en aurait Ã©tÃ© la consÃ©quence sur les autres lignes ?

- **Commandes SQL nÃ©cessaires :**
  1. Modifier le schÃ©ma de la table pour ajouter la colonne :
     ```sql
     ALTER TABLE jeux ADD COLUMN nb_votes INTEGER;
     ```
  2. Mettre Ã  jour la ligne ciblÃ©e :
     ```sql
     UPDATE jeux SET nb_votes = 0 WHERE titre = 'Terraria';
     ```
- **ConsÃ©quence sur les autres lignes :**
  - En modÃ¨le relationnel Ã  schÃ©ma fixe, la nouvelle colonne est ajoutÃ©e Ã  **toutes les lignes existantes** de la table.
  - Toutes les autres lignes reÃ§oivent obligatoirement la valeur `NULL` (ou la valeur par dÃ©faut dÃ©finie), consommant de l'espace de stockage et imposant la gestion des valeurs nulles dans l'application, mÃªme si ce champ n'a aucun sens pour d'autres jeux. En MongoDB, le champ n'existe que sur le document modifiÃ©.

---

## Q8. Lisez `IGameCatalog`. Y a-t-il un seul endroit dans cette interface oÃ¹ le mot Â« Mongo Â» apparaÃ®t ? Pourquoi est-ce important, Ã  votre avis ?

- **Constat :**
  - Non, le terme Â« Mongo Â» n'apparaÃ®t Ã  aucun endroit dans `IGameCatalog`. L'interface ne manipule que des abstractions C# et le modÃ¨le mÃ©tier `Game` (`Task<List<Game>>`, `string genre`, `int count`, etc.).
- **Pourquoi c'est important :**
  - **Principe d'Inversion des DÃ©pendances (DIP) / Clean Architecture :** Les couches supÃ©rieures (les endpoints d'API dans `Program.cs`) dÃ©pendent d'un contrat (l'interface) et non d'une implÃ©mentation concrÃ¨te.
  - **DÃ©couplage technologique :** L'application ne sait pas comment ni oÃ¹ sont stockÃ©s les jeux. Si on dÃ©cide plus tard de remplacer MongoDB par PostgreSQL, un cache Redis, ou un service mockÃ© pour des tests unitaires, l'interface `IGameCatalog` et les endpoints restent inchangÃ©s. Seule la classe d'implÃ©mentation injectÃ©e dans le conteneur IoC est modifiÃ©e.


---

## Q9. Quelle exception obtenez-vous, et sur quel champ ? Expliquez la cause : qu'est-ce que le driver a essayÃ© de faire, et pourquoi n'a-t-il pas su ?

- **Exception obtenue :**
  ```text
  System.FormatException: Element 'joueursParEquipe' does not match any field or property of class PixelHub.Api.Models.Game.
  ```
- **Sur quel champ :** Le champ `joueursParEquipe` (prÃ©sent dans le document de *Counter-Strike 2*).
- **Cause :**
  - Le driver C# MongoDB tente de dÃ©sÃ©rialiser le document BSON retournÃ© par MongoDB vers une instance de la classe `Game`.
  - Par dÃ©faut, le sÃ©rialiseur BSON C# applique une politique stricte : chaque champ prÃ©sent dans le document BSON doit possÃ©der une propriÃ©tÃ© ou un champ correspondant dans la classe C#.
  - DÃ¨s qu'il rencontre un champ non dÃ©clarÃ© dans la classe `Game` (`joueursParEquipe`), le driver ne sait pas oÃ¹ stocker cette valeur et lÃ¨ve immÃ©diatement une `FormatException` pour signaler une incohÃ©rence entre le document et le modÃ¨le.

---

## Q10. `[BsonIgnoreExtraElements]` fait disparaÃ®tre l'erreur, mais au prix de quoi ? Que devient `joueursParEquipe` quand vous appelez `/games` ?

- **Au prix de quoi :**
  - Cela se fait au prix d'une **perte d'information** au niveau applicatif. Tout champ prÃ©sent dans MongoDB mais non dÃ©clarÃ© dans la classe C# est silencieusement ignorÃ© et jetÃ© lors de la dÃ©sÃ©rialisation.
- **Que devient `joueursParEquipe` :**
  - Le champ `joueursParEquipe` n'est pas chargÃ© en mÃ©moire dans l'objet `Game`.
  - Par consÃ©quent, il est totalement absent du flux JSON retournÃ© par l'endpoint `/games`.

---

## Q11. Comparez les trois approches possibles pour gÃ©rer un schÃ©ma variable en C# : `[BsonIgnoreExtraElements]`, `[BsonExtraElements]`, et une hiÃ©rarchie de classes (`FpsGame : Game`, etc.). Quel est l'avantage et l'inconvÃ©nient de chacune ?

1. **`[BsonIgnoreExtraElements]`** :
   - **Avantage :** Solution la plus simple et directe. L'application reste stable face aux variations de schÃ©ma et ne mappe que le sous-ensemble commun de champs dont elle a besoin.
   - **InconvÃ©nient :** Perte de donnÃ©es : les champs spÃ©cifiques aux documents ne peuvent pas Ãªtre exploitÃ©s, modifiÃ©s ou rÃ©Ã©crits par l'application.
2. **`[BsonExtraElements]` (avec un dictionnaire / BsonDocument dynamique)** :
   - **Avantage :** Conserve toutes les donnÃ©es non dÃ©clarÃ©es sans perte. Permet d'exposer dynamiquement les champs spÃ©cifiques dans l'API (via un dictionnaire).
   - **InconvÃ©nient :** Perte du typage fort C# sur ces champs (manipulation de clÃ©s/valeurs gÃ©nÃ©riques `object` ou `BsonDocument`), et complexitÃ© de conversion entre la sÃ©rialisation BSON et le sÃ©rialiseur JSON de l'API (`[JsonIgnore]`, `ToDictionary()`).
3. **HiÃ©rarchie de classes (`FpsGame : Game`, `GestionGame : Game`, avec discriminateur BSON)** :
   - **Avantage :** Typage fort complet, propretÃ© du POCO, validation Ã  la compilation et autocomplÃ©tion pour chaque famille de jeu.
   - **InconvÃ©nient :** RigiditÃ© accrue : chaque nouvelle variante nÃ©cessite de coder et maintenir une nouvelle classe C#. Moins adaptÃ© si les documents Ã©voluent continuellement ou sont trÃ¨s imprÃ©visibles.

---

## Q12. Ã‰crivez la requÃªte SQL qui produirait le mÃªme rÃ©sultat sur une table `jeux(titre, genre, note)`. Comparez les deux. Est-ce ici que MongoDB apporte quelque chose par rapport Ã  PostgreSQL ? Si non, oÃ¹ l'apport se situe-t-il dans ce TP ?

- **RequÃªte SQL Ã©quivalente :**
  ```sql
  SELECT genre, AVG(note) AS "NoteMoyenne", COUNT(*) AS "Nombre"
  FROM jeux
  GROUP BY genre
  ORDER BY "NoteMoyenne" DESC;
  ```
- **Comparaison :**
  - La requÃªte SQL est plus lisible, dÃ©clarative et naturelle pour effectuer des calculs statistiques groupÃ©s (`GROUP BY`, `AVG`, `COUNT`).
  - Le pipeline d'agrÃ©gation MongoDB en C# (`Aggregate().Group(...).SortByDescending(...)`) est plus impÃ©ratif et verbeux.
- **MongoDB apporte-t-il quelque chose ici ?**
  - **Non**, sur les agrÃ©gations tabulaires classiques, MongoDB n'apporte rien de supÃ©rieur Ã  PostgreSQL, qui dispose d'un optimiseur de requÃªtes relationnelles extrÃªmement performant et Ã©prouvÃ©.
  - **OÃ¹ se situe le rÃ©el apport de MongoDB dans ce TP :**
    1. **Le polymorphisme / schÃ©ma flexible :** Pouvoir stocker des jeux aux attributs trÃ¨s disparates dans une mÃªme collection sans colonnes `NULL` superflues.
    2. **Les types complexes et collections imbriquÃ©es :** La gestion native des tableaux (`plateformes`, `tags`, `cartes`) sans tables de jointure n:m.

---

## Question orale de l'enseignant : *Â« Les avis d'un jeu : tu imbriques ou tu rÃ©fÃ©rencies ? Â»*

- **RÃ©ponse argumentÃ©e :**
  - **On imbrique (Embedding) si :**
    - Le nombre d'avis reste modÃ©rÃ© et bornÃ© (pour ne pas risquer d'atteindre la limite technique de 16 Mo par document BSON).
    - Les avis sont quasi systÃ©matiquement affichÃ©s en mÃªme temps que la fiche dÃ©taillÃ©e du jeu (lecture conjointe frÃ©quente en 1 seule opÃ©ration d'E/S).
  - **On rÃ©fÃ©rence (Referencing avec une collection `avis` distincte) si :**
    - Le volume d'avis peut exploser (des milliers d'avis par jeu), ce qui alourdirait le document `Game` lors des requÃªtes simples (comme lister les jeux du catalogue).
    - Les avis ont leur propre cycle de vie (pagination, votes sur les avis, modÃ©ration indÃ©pendante, analyse statistique globale des utilisateurs).
  - **Conclusion :** Dans un systÃ¨me rÃ©aliste comme PixelHub avec une communautÃ© active, le **rÃ©fÃ©rencement** (ou une approche hybride avec les derniers avis imbriquÃ©s et l'historique archivÃ©/rÃ©fÃ©rencÃ©) est gÃ©nÃ©ralement prÃ©fÃ©rable.


---
---

# TP 2 â€” Pour aller plus loin (Bonus)

## 1. Index et mesure (`explain("executionStats")`)

- **Avant la crÃ©ation de l'index :**
  - RequÃªte : `db.jeux.find({ genre: "FPS" }).explain("executionStats")`
  - Ã‰tape d'exÃ©cution : `COLLSCAN` (Collection Scan, parcours sÃ©quentiel complet).
  - Documents examinÃ©s : `totalDocsExamined = 11`.
  - Documents retournÃ©s : `nReturned = 2`.
  - Temps d'exÃ©cution : ~21 ms.
- **CrÃ©ation de l'index :**
  ```javascript
  db.jeux.createIndex({ genre: 1 })
  ```
- **AprÃ¨s la crÃ©ation de l'index :**
  - Ã‰tape d'exÃ©cution : `IXSCAN` (Index Scan) suivi de `FETCH`.
  - ClÃ©s d'index examinÃ©es : `totalKeysExamined = 2`.
  - Documents examinÃ©s : `totalDocsExamined = 2`.
  - Documents retournÃ©s : `nReturned = 2`.
  - Temps d'exÃ©cution : ~8 ms.
- **Constat sur 10 documents et explication :**
  - Sur un jeu de donnÃ©es aussi rÃ©duit (10-11 documents), le gain rÃ©el est imperceptible (le parcours complet en RAM est instantanÃ©). L'index introduit mÃªme une structure mÃ©moire supplÃ©mentaire et un lÃ©ger surcoÃ»t Ã  l'Ã©criture.
  - En revanche, sur une base de production contenant plusieurs millions de jeux, l'index est indispensable : il transforme une recherche linÃ©aire en $O(N)$ (qui saturerait le CPU et le disque) en une recherche logarithmique en $O(\log N)$.

---

## 2. ModÃ©liser les avis (Documents imbriquÃ©s)

- **Ajout des avis en imbriquÃ© :**
  ```javascript
  db.jeux.updateOne(
    { titre: "Baldur's Gate 3" },
    {
      $set: {
        avis: [
          { auteur: "Alice", note: 5, commentaire: "Chef d'Å“uvre absolu !" },
          { auteur: "Bob", note: 4, commentaire: "TrÃ¨s bon mais chronophage." }
        ]
      }
    }
  );
  ```
- **RequÃªte renvoyant les jeux ayant au moins un avis avec la note 5 :**
  ```javascript
  db.jeux.find({ "avis.note": 5 }, { titre: 1, avis: 1, _id: 0 })
  ```
  *(Ou avec `$elemMatch` si l'on a plusieurs critÃ¨res sur le mÃªme sous-document : `db.jeux.find({ avis: { $elemMatch: { note: 5 } } })`)*.
- **RÃ©sultat :** Renvoie bien *Baldur's Gate 3*. GrÃ¢ce au modÃ¨le orientÃ© document, on filtre directement Ã  l'intÃ©rieur d'un tableau d'objets imbriquÃ©s sans jointure.

---

## 3. Validation de schÃ©ma (`$jsonSchema`)

- **Commande pour imposer un champ `titre` de type chaÃ®ne obligatoire :**
  ```javascript
  db.runCommand({
    collMod: "jeux",
    validator: {
      $jsonSchema: {
        bsonType: "object",
        required: ["titre"],
        properties: {
          titre: {
            bsonType: "string",
            description: "doit Ãªtre une chaÃ®ne de caractÃ¨res et est obligatoire"
          }
        }
      }
    },
    validationLevel: "moderate"
  });
  ```
- **Test d'insertion invalide (sans titre) :**
  ```javascript
  db.jeux.insertOne({ genre: "Invalide Sans Titre", note: 1.0 })
  ```
- **RÃ©sultat observÃ© :**
  - L'insertion est rejetÃ©e avec l'erreur `MongoServerError: Document failed validation`.
  - DÃ©tail de l'erreur : `missingProperties: [ 'titre' ]` sur la rÃ¨gle `$jsonSchema`.
  - Cela permet de sÃ©curiser le schÃ©ma au niveau du moteur tout en conservant la souplesse de MongoDB sur les autres attributs.

---

## Question d'ouverture pour la sÃ©ance 3

> *Â« PixelHub doit afficher le nombre de joueurs actuellement en ligne, mis Ã  jour chaque seconde. Vous le stockez dans PostgreSQL, ou dans MongoDB ? Â»*

- **RÃ©ponse :** Dans **aucun des deux** !
- **Moteur adaptÃ© :** On utilise **Redis** (qui sera vu Ã  la sÃ©ance 3).
- **Pourquoi :**
  - Ã‰crire et mettre Ã  jour un compteur chaque seconde dans PostgreSQL ou MongoDB entraÃ®nerait des Ã©critures disque et des verrous transactionnels inutiles pour une donnÃ©e trÃ¨s Ã©phÃ©mÃ¨re.
  - Redis stocke toutes ses donnÃ©es en mÃ©moire vive (RAM) et propose des opÃ©rations atomiques instantanÃ©es en $O(1)$ (comme `INCR` / `DECR`), ce qui en fait le moteur idÃ©al pour les compteurs temps rÃ©el et les donnÃ©es de prÃ©sence.

---
---

# TP 3 â€” Classements, matchmaking et cache avec Redis

## Partie 1 â€” Les structures dans `redis-cli`

### Q1. `INCR` est une opÃ©ration atomique. Qu'est-ce que Ã§a veut dire concrÃ¨tement, et pourquoi est-ce important si mille joueurs terminent une partie en mÃªme temps ? Comment auriez-vous fait la mÃªme chose en SQL, et quelle diffÃ©rence cela fait-il ?

- **Signification concrÃ¨te de l'atomicitÃ© :**
  - Une opÃ©ration est atomique lorsqu'elle s'exÃ©cute en une Ã©tape indivisible et ininterruptible. Aucun autre client ne peut observer ou intercaler une opÃ©ration sur un Ã©tat intermÃ©diaire.
- **Importance face Ã  1 000 joueurs simultanÃ©s :**
  - Redis traite les commandes de maniÃ¨re sÃ©quentielle sur une boucle d'Ã©vÃ©nements monothreadÃ©e (*single-threaded event loop*).
  - 1 000 requÃªtes `INCR` arrivant en mÃªme temps sont traitÃ©es les unes aprÃ¨s les autres sans aucune condition de concurrence (*race condition*). Le compteur passera de maniÃ¨re garantie et exacte de $N$ Ã  $N + 1000$.
- **Ã‰quivalent en SQL :**
  - En SQL, on Ã©crirait :
    ```sql
    UPDATE parties SET compteur = compteur + 1 WHERE id = 1;
    ```
- **DiffÃ©rence majeure :**
  - En SQL relationnel, cette opÃ©ration pose un **verrou exclusif de ligne** (*Row Lock*), Ã©crit dans le journal des transactions (*WAL*) et provoque des Ã©critures disque. Si 1 000 requÃªtes concurrentes frappent cette mÃªme ligne, elles s'empilent en attente du verrou, Ã©croulant le dÃ©bit du serveur.
  - Dans Redis, l'opÃ©ration s'effectue purement en mÃ©moire vive (RAM) en quelques nanosecondes sans verrou transactionnel lourd.

---

### Q2. On aurait pu sÃ©rialiser tout le joueur en JSON dans une simple `String`. DÃ©crivez les Ã©tapes qu'il aurait alors fallu faire pour ajouter 250 XP, et le problÃ¨me qui apparaÃ®t si deux requÃªtes le font en mÃªme temps.

- **Ã‰tapes nÃ©cessaires avec du JSON dans une `String` :**
  1. Lire la chaÃ®ne JSON complÃ¨te depuis Redis via le rÃ©seau : `GET joueur:7`.
  2. DÃ©sÃ©rialiser la chaÃ®ne JSON en objet applicatif (C# / POCO).
  3. Modifier la propriÃ©tÃ© en mÃ©moire : `joueur.Xp += 250`.
  4. ResÃ©rialiser l'objet complet en JSON.
  5. RÃ©Ã©crire toute la chaÃ®ne dans Redis : `SET joueur:7 "..."`.
- **ProblÃ¨me de concurrence (*Lost Update / Race Condition*) :**
  - Si deux requÃªtes concurrentes $A$ et $B$ tentent d'ajouter 250 XP simultanÃ©ment :
    - $A$ lit le JSON (`xp: 3400`).
    - $B$ lit le JSON avant que $A$ n'ait sauvegardÃ© (`xp: 3400`).
    - $A$ calcule 3650 et fait son `SET`.
    - $B$ calcule 3650 et fait son `SET`.
    - **RÃ©sultat :** Le joueur a 3650 XP au lieu de 3900 XP. La mise Ã  jour de $A$ a Ã©tÃ© Ã©crasÃ©e et perdue.
- **Avantage du `Hash` :**
  - Avec `HINCRBY joueur:7 xp 250`, l'opÃ©ration est exÃ©cutÃ©e directement et atomiquement sur le champ cible par le moteur Redis, sans nÃ©cessiter de rapatriement rÃ©seau ni de dÃ©sÃ©rialisation de l'objet complet.

---

### Q3. Les deux derniÃ¨res commandes (`ZREVRANGE` et `ZRANGE`) ne renvoient pas la mÃªme chose. Expliquez la diffÃ©rence, et dites laquelle vous utiliserez pour afficher un Â« top 3 Â».

- **DiffÃ©rence :**
  - Dans un Sorted Set (`ZSet`), les membres sont ordonnÃ©s par dÃ©faut selon leur score de maniÃ¨re **croissante** (du plus petit au plus grand score).
  - `ZRANGE classement:global 0 2 WITHSCORES` parcourt du dÃ©but vers la fin (ordre croissant) et renvoie les 3 moins bons scores : `Pixel (1800)`, `Krayz (3200)`, `Nova (4500)`.
  - `ZREVRANGE classement:global 0 2 WITHSCORES` (ou `ZRANGE ... REV`) inverse le sens de parcours et renvoie les Ã©lÃ©ments par ordre **dÃ©croissant** : `Ombre (5100)`, `Nova (4500)`, `Krayz (3200)`.
- **Pour afficher un Â« top 3 Â» :**
  - On utilise obligatoirement **`ZREVRANGE`** (ou `ZRANGE REV`), car un classement valorise les joueurs possÃ©dant les scores les plus Ã©levÃ©s aux premiÃ¨res positions.

---

### Q4. `ZREVRANK` renvoie un nombre. Si vous affichez ce nombre tel quel au joueur, que va-t-il lire ? Que faut-il faire ?

- **Ce que lirait le joueur :**
  - `ZREVRANK` renvoie un index informatique basÃ© sur zÃ©ro (*0-based index*).
  - Si le joueur est le meilleur (comme `Krayz` aprÃ¨s avoir reÃ§u 2000 points avec son score de 5200), la commande renvoie `0`.
  - AffichÃ© tel quel, le joueur lirait qu'il est **Â« 0e Â»**, ce qui est anormal pour un Ãªtre humain.
- **Ce qu'il faut faire :**
  - Il faut systÃ©matiquement **ajouter 1** Ã  la valeur retournÃ©e (`rang = index + 1`) dans la couche applicative C# avant de l'exposer ou de l'afficher.

---

### Q5. Qui sort de la file au `RPOP`, et pourquoi ? Qu'aurait donnÃ© `LPOP` Ã  la place ?

- **Qui sort au `RPOP` :**
  - C'est **"Nova"** qui sort.
- **Pourquoi :**
  - Les joueurs ont Ã©tÃ© ajoutÃ©s par la gauche avec `LPUSH` dans l'ordre : `"Nova"`, puis `"Krayz"`, puis `"Ombre"`.
  - L'Ã©tat interne de la liste est donc `["Ombre", "Krayz", "Nova"]`.
  - `RPOP` dÃ©pile par la droite (la queue de la liste). Nova Ã©tant entrÃ©e la premiÃ¨re, elle est la premiÃ¨re Ã  sortir. Cela respecte le principe **FIFO** (*First-In, First-Out*), fondamental pour une file d'attente de matchmaking Ã©quitable.
- **Ce qu'aurait donnÃ© `LPOP` :**
  - `LPOP` aurait dÃ©pilÃ© par la gauche (la tÃªte de la liste), extrayant **"Ombre"** (le dernier joueur entrÃ©). Cela aurait formÃ© une pile **LIFO** (*Last-In, First-Out*), pÃ©nalisant les joueurs ayant attendu le plus longtemps.

---

### Q6. Quelles valeurs obtenez-vous ? Que renvoie `TTL` sur une clÃ© sans expiration (testez avec `compteur:parties`) ? Que signifie chacune des valeurs de retour ?

- **Valeurs obtenues :**
  - ImmÃ©diatement aprÃ¨s crÃ©ation avec `EX 30` : `TTL enligne:Nova` renvoie un entier positif (ex. `30` ou `28`), et `EXISTS` renvoie `1`.
  - AprÃ¨s Ã©coulement des 30 secondes : `TTL enligne:Nova` renvoie **`-2`**, et `EXISTS` renvoie **`0`**.
  - Sur une clÃ© sans expiration (`compteur:parties`) : `TTL` renvoie **`-1`**.
- **Signification des trois valeurs de retour de `TTL` :**
  - **Entier positif ($> 0$) :** Nombre de secondes restantes avant la destruction programmÃ©e de la clÃ©.
  - **`-1` :** La clÃ© existe bien en mÃ©moire, mais **aucune durÃ©e de vie ne lui est associÃ©e** (clÃ© persistante).
  - **`-2` :** La clÃ© **n'existe pas** (ou a expirÃ© et a Ã©tÃ© purgÃ©e de la mÃ©moire).

---

### Q7. Personne n'a lancÃ© de tÃ¢che de nettoyage. Quel code auriez-vous dÃ» Ã©crire pour obtenir le mÃªme comportement avec PostgreSQL, et quels risques ce code introduit-il ?

- **Code nÃ©cessaire dans PostgreSQL :**
  1. Ajouter une colonne horodatÃ©e sur la table des sessions : `derniere_activite TIMESTAMP`.
  2. DÃ©velopper et hÃ©berger un processus d'arriÃ¨re-plan externe (cron OS, `BackgroundService` .NET, ou extension SQL `pg_cron`) exÃ©cutant Ã  intervalle rÃ©gulier :
     ```sql
     DELETE FROM connexions WHERE derniere_activite < NOW() - INTERVAL '30 seconds';
     ```
- **Risques introduits par cette approche :**
  1. **Contention et surcharge disque :** Des `DELETE` frÃ©quents gÃ©nÃ¨rent des verrous, polluent le WAL et crÃ©ent des lignes mortes (*dead tuples*), obligeant Ã  des `VACUUM` frÃ©quents.
  2. **VulnÃ©rabilitÃ© aux pannes (*Silent Failure*) :** Si le processus de nettoyage externe plante ou est mal planifiÃ©, les lignes s'accumulent silencieusement et saturent l'espace disque.
  3. **IncohÃ©rence temporelle :** Entre deux cycles du nettoyeur, des donnÃ©es thÃ©oriquement expirÃ©es restent visibles et faussent les comptages si chaque requÃªte n'ajoute pas un filtre `WHERE derniere_activite >= ...`.
  - **Avantage Redis :** Le TTL est gÃ©rÃ© de faÃ§on native et autonome au cÅ“ur du moteur (nettoyage passif Ã  l'accÃ¨s + nettoyage actif probabiliste).

---
---

## Partie 2 â€” Redis dans PixelHub

### Q8. Ce service est enregistrÃ© en singleton (`builder.Services.AddSingleton<IConnectionMultiplexer>`). Cherchez dans la documentation de StackExchange.Redis ce qu'est un `ConnectionMultiplexer` et ce qui se passerait si on en crÃ©ait un Ã  chaque requÃªte HTTP. Pourquoi ce bug serait-il invisible aujourd'hui en TP ?

- **Qu'est-ce qu'un `ConnectionMultiplexer` :**
  - C'est l'objet pivot de la bibliothÃ¨que `StackExchange.Redis`. Il encapsule et gÃ¨re la connexion rÃ©seau sous-jacente au serveur Redis.
  - Il a Ã©tÃ© spÃ©cifiquement conÃ§u pour Ãªtre **thread-safe** et partagÃ© : il implÃ©mente un multiplexage de sockets TCP, permettant Ã  de multiples threads concurrents d'acheminer leurs commandes asynchrones Ã  travers une mÃªme connexion persistante hautement optimisÃ©e.
- **Ce qui se passerait si on en crÃ©ait un Ã  chaque requÃªte HTTP (ex. en Transient ou Scoped) :**
  - Chaque requÃªte HTTP forcerait l'ouverture d'un nouveau socket TCP, le handshake rÃ©seau et la nÃ©gociation TLS/auth.
  - En refermant la connexion, les sockets entreraient en Ã©tat d'attente prolongÃ©e `TIME_WAIT` au niveau du noyau de l'OS.
  - Sous trafic rÃ©el, cela mÃ¨nerait immÃ©diatement Ã  l'**Ã©puisement des sockets/ports locaux** (*Socket / Port Exhaustion*), provoquant des erreurs `SocketException: No buffer space available` ou `RedisTimeoutException`, une fuite mÃ©moire et un effondrement des performances.
- **Pourquoi ce bug serait invisible aujourd'hui en TP :**
  - En environnement local de TP, nous n'exÃ©cutons que quelques requÃªtes manuelles sÃ©quentielles (quelques dizaines tout au plus).
  - Le systÃ¨me d'exploitation dispose de milliers de ports Ã©phÃ©mÃ¨res libres et n'est soumis Ã  aucune concurrence massive. Le bug d'Ã©puisement de sockets ne se manifeste qu'Ã  partir d'un trafic soutenu de plusieurs dizaines ou centaines de requÃªtes par seconde.

---

### Q9. Supprimez `Order.Descending` des deux mÃ©thodes, relancez, et regardez `/leaderboard/top/5`. Le rÃ©sultat est-il visiblement faux ? Pourquoi ce bug est-il particuliÃ¨rement dangereux ?

- **RÃ©sultat observÃ© :**
  - En omettant `Order.Descending`, Redis applique son ordre naturel par dÃ©faut qui est **croissant** (les scores les plus bas en tÃªte).
  - L'API retourne les 5 joueurs ayant les plus faibles scores (ex. `Pixel` avec 1800 points en premier).
- **Le rÃ©sultat est-il visiblement faux ?**
  - **Non**, au premier regard le rÃ©sultat paraÃ®t parfaitement valide : l'API rÃ©pond avec un code HTTP 200, un tableau JSON bien formÃ© de 5 joueurs avec leurs scores et des rangs 1, 2, 3, 4, 5. Aucun message d'erreur ni exception n'est levÃ©.
- **Pourquoi ce bug est particuliÃ¨rement dangereux :**
  - Il s'agit d'un bug **sÃ©mantique / logique silencieux**. Tout le pipeline technique fonctionne, mais le classement est inversÃ© : le Â« Top 5 Â» devient un Â« Flop 5 Â».
  - Dans un systÃ¨me de jeu en production, une telle inversion distribuerait indÃ»ment les rÃ©compenses de fin de saison, fausserait l'algorithme de matchmaking et dÃ©truirait l'intÃ©gritÃ© compÃ©titive sans qu'aucune sonde de monitoring technique (alertes d'erreur 500, crash logs) ne s'allume.

---

### Q10. Comment un joueur sort-il de cet ensemble (`Set`) quand il ferme son navigateur sans se dÃ©connecter ? Cherchez la commande Redis qui ferait expirer un seul membre d'un Set. Que trouvez-vous ?

- **Sortie d'un `Set` en cas de fermeture brutale du navigateur :**
  - Le joueur **ne sort jamais** de l'ensemble. Il y reste prÃ©sent indÃ©finiment car aucune requÃªte HTTP de dÃ©connexion n'est Ã©mise.
- **Commande Redis pour faire expirer un seul membre d'un Set :**
  - Il n'existe **absolument aucune commande** dans Redis permettant d'affecter une durÃ©e de vie (TTL) Ã  un membre individuel d'un `Set` (ni d'ailleurs Ã  un champ individuel d'un `Hash` ou Ã  un Ã©lÃ©ment d'une `List`).
  - La commande `EXPIRE` s'applique strictement et exclusivement Ã  **la clÃ© complÃ¨te** dans Redis.
  - Par consÃ©quent, la structure `Set` standard est incapable de modÃ©liser un systÃ¨me de prÃ©sence dynamique avec expiration sans intervention manuelle.

---

### Q11. DÃ©crivez votre solution avant de la coder : quelle structure, quelle clÃ©, que met-on dans le score, et quelles commandes pour compter et pour purger ?

- **Structure choisie :** Le **Sorted Set** (`ZSet`).
- **Nom de la clÃ© :** `enligne:joueurs`.
- **Membre :** Le pseudo du joueur (`pseudo`).
- **Valeur du Score :** Le **timestamp Epoch Unix en secondes** (`DateTimeOffset.UtcNow.ToUnixTimeSeconds()`) correspondant Ã  l'horodatage de sa derniÃ¨re activitÃ© (*heartbeat*).
- **Commandes utilisÃ©es :**
  1. **Enregistrer un signe de vie (`Touch`) :**
     - Commande : `ZADD enligne:joueurs <timestamp_actuel> <pseudo>`
     - MÃ©thode C# : `_db.SortedSetAddAsync("enligne:joueurs", pseudo, now)`
     - *Effet :* Ajoute le joueur s'il est nouveau, ou met Ã  jour son score (son dernier horodatage) s'il est dÃ©jÃ  prÃ©sent.
  2. **Purger les joueurs inactifs :**
     - Commande : `ZREMRANGEBYSCORE enligne:joueurs 0 (<timestamp_actuel> - 60)`
     - MÃ©thode C# : `_db.SortedSetRemoveRangeByScoreAsync("enligne:joueurs", 0, now - 60)`
     - *Effet :* Supprime en une seule opÃ©ration $O(\log N + M)$ tous les membres dont le dernier signe de vie date de plus de 60 secondes.
  3. **Compter les joueurs en ligne :**
     - Commande : `ZCARD enligne:joueurs`
     - MÃ©thode C# : `_db.SortedSetLengthAsync("enligne:joueurs")`
     - *Effet :* Retourne instantanÃ©ment la cardinalitÃ© de l'ensemble aprÃ¨s la purge.

---

### Q12. Combien de lignes avez-vous modifiÃ©es dans vos endpoints pour ajouter ce cache ? Pourquoi ? Quel choix de la sÃ©ance 2 rend cela possible ?

- **Nombre de lignes modifiÃ©es dans les endpoints :** **0 ligne** (strictement aucune).
- **Pourquoi :**
  - GrÃ¢ce au respect du **Principe d'Inversion des DÃ©pendances (DIP)** et Ã  l'application du **Pattern Decorator**.
  - Les endpoints minimalistes de `Program.cs` (`app.MapGet("/games", async (IGameCatalog catalog) => ...)`) ne dÃ©pendent que de l'interface abstraite `IGameCatalog`.
  - La classe `CachedGameCatalog` implÃ©mente ce mÃªme contrat `IGameCatalog` et prend en dÃ©pendance l'implÃ©mentation originale `MongoGameCatalog`. Lors de l'enregistrement dans le conteneur IoC :
    ```csharp
    builder.Services.AddSingleton<MongoGameCatalog>();
    builder.Services.AddSingleton<IGameCatalog>(sp =>
        new CachedGameCatalog(
            sp.GetRequiredService<MongoGameCatalog>(),
            sp.GetRequiredService<IConnectionMultiplexer>()));
    ```
    L'injection de dÃ©pendances substitue de faÃ§on transparente l'instance concrÃ¨te par le proxy de cache sans toucher au code des endpoints.
- **Choix de la sÃ©ance 2 rendant cela possible :**
  - L'introduction dÃ¨s le TP 2 de l'interface `IGameCatalog` pour abstraire l'accÃ¨s aux donnÃ©es MongoDB, plutÃ´t que d'injecter directement `IMongoDatabase` ou une classe concrÃ¨te dans les handlers HTTP.

---
---

## Partie 3 â€” Mesurer, et se mÃ©fier de la mesure

### Q13. Relevez vos mesures. Le gain est-il spectaculaire ? Quelle conclusion honnÃªte pouvez-vous en tirer, et dans quelles conditions un cache devient-il rÃ©ellement dÃ©cisif ?

- **Mesures relevÃ©es sur `/games/timed` :**
  - **1er appel (Cache Miss â€” lecture MongoDB + Ã©criture Redis) :** **~5.54 ms**
  - **2e appel (Cache Hit â€” lecture directe RAM Redis) :** **~0.82 ms**
  - **3e appel (Cache Hit) :** **~0.58 ms**
  - **4e et 5e appels (Cache Hit) :** **~0.37 ms**
- **Le gain est-il spectaculaire ?**
  - En valeur relative : **Oui**, le temps de rÃ©ponse est divisÃ© par $\approx 15$ (gain de 93% du temps de traitement).
  - En valeur absolue : **Non**, passer de 5,5 ms Ã  0,4 ms est totalement imperceptible pour un Ãªtre humain sur un Ã©cran.
- **Conclusion honnÃªte :**
  - Sur un petit volume local (11 documents MongoDB stockÃ©s sur un SSD local), MongoDB est dÃ©jÃ  si vÃ©loce que l'ajout d'une couche de cache Redis n'apporte aucun bÃ©nÃ©fice tangible pour l'utilisateur final.
- **Conditions oÃ¹ le cache devient rÃ©ellement dÃ©cisif :**
  1. **RequÃªtes lourdes et agrÃ©gations complexes :** RequÃªtes analytiques balayant des millions de documents ou rÃ©alisant des calculs d'agrÃ©gation groupÃ©s qui prennent plusieurs centaines de millisecondes ou secondes en base.
  2. **Trafic massif et protection de la base (Effet bouclier) :** Sous une charge de 10 000 requÃªtes/seconde, MongoDB saturerait ses connexions et ses entrÃ©es/sorties disque. Redis absorbe 99% du trafic en RAM sans solliciter le moteur de persistance.
  3. **Latence rÃ©seau inter-serveurs :** Lorsque l'application et la base de donnÃ©es sont sur des machines ou des rÃ©gions cloud distinctes (le cache local ou in-memory Ã©limine le RTT rÃ©seau vers la base distante).

---

### Q14. Un administrateur corrige le titre d'un jeu directement dans MongoDB. Combien de temps les joueurs verront-ils l'ancien titre, et pourquoi ? Proposez deux stratÃ©gies pour rÃ©duire ce dÃ©lai, avec leur inconvÃ©nient.

- **DurÃ©e de visibilitÃ© de l'ancien titre :** Jusqu'Ã  **60 secondes** (la durÃ©e du TTL paramÃ©trÃ© dans `CachedGameCatalog`).
- **Pourquoi :**
  - Dans le motif *Cache-Aside*, l'API interroge Redis en premier. Tant que la clÃ© `cache:catalogue:tous` est prÃ©sente en RAM, Redis retourne le JSON prÃ©alablement mis en cache sans jamais interroger MongoDB. La modification en base reste donc invisible jusqu'Ã  ce que la clÃ© expire d'elle-mÃªme.
- **StratÃ©gie 1 : Invalidation active Ã  l'Ã©criture (*Cache Eviction*)**
  - *Principe :* Chaque fois qu'un endpoint de modification ou d'ajout de jeu est appelÃ© (`PUT/POST/DELETE /games`), l'API supprime explicitement la clÃ© de cache (`await _cache.KeyDeleteAsync("cache:catalogue:tous")`). Le prochain appel sera un *cache miss* et rechargera la donnÃ©e Ã  jour depuis MongoDB.
  - *InconvÃ©nient :* Si la modification est effectuÃ©e **directement en base** par un administrateur (via `mongosh`, Compass ou un script SQL/BSON sans passer par l'API), le cache ne le saura jamais et restera dÃ©synchronisÃ© jusqu'au TTL. De plus, cela couple la logique d'Ã©criture avec la gestion du cache.
- **StratÃ©gie 2 : TTL dynamique plus court ou Change Streams (CDC)**
  - *Option A (TTL plus court) :* RÃ©duire le TTL Ã  5 secondes. *InconvÃ©nient :* Augmente considÃ©rablement le taux de *cache miss* et sollicite davantage MongoDB.
  - *Option B (Change Streams) :* Configurer un listener d'Ã©vÃ©nements MongoDB (*Change Streams*) dans l'API C# qui Ã©coute les modifications au niveau de l'Oplog MongoDB en temps rÃ©el et purge automatiquement la clÃ© Redis correspondante. *InconvÃ©nient :* Architecture complexe Ã  maintenir, consomme des ressources de streaming et nÃ©cessite un cluster / Replica Set MongoDB actif.

---

### Question orale de l'enseignant : *Â« Le solde de monnaie virtuelle d'un joueur : tu le mettrais dans Redis ? Â»*

- **RÃ©ponse argumentÃ©e :**
  - **Absolument NON.**
  - Le solde de monnaie virtuelle d'un joueur reprÃ©sente un actif critique (valeur monÃ©taire ou contractuelle). Il exige des garanties strictes de **durabilitÃ© (ACID)** : chaque transaction doit Ãªtre validÃ©e, protÃ©gÃ©e contre les Ã©critures concurrentes et immÃ©diatement Ã©crite sur disque dans un journal des transactions (*Write-Ahead Log*) pour survivre Ã  n'importe quel crash matÃ©riel ou coupure d'alimentation.
  - Dans notre infrastructure polyglotte, **Redis est configurÃ© sans volume Docker de persistance** : toutes ses donnÃ©es rÃ©sident en RAM volatile. Si le conteneur Redis redÃ©marre ou subit un dÃ©passement de mÃ©moire (OOM), l'intÃ©gralitÃ© des donnÃ©es en cache est perdue.
  - Par consÃ©quent, le solde doit impÃ©rativement Ãªtre gÃ©rÃ© par **PostgreSQL**.
  - Redis est rÃ©servÃ© aux donnÃ©es Ã©phÃ©mÃ¨res (prÃ©sence en ligne), reconstructibles (classements dÃ©rivÃ©s des parties, cache catalogue) ou dont la perte n'entraÃ®ne aucun prÃ©judice d'intÃ©gritÃ©.

