# Réponses aux questions d'analyse du fichier `docker-compose.yml`

## Q1. Le fichier déclare quatre services alors que le TP d'aujourd'hui n'en utilise qu'un. Pourquoi, à votre avis ?

- **Continuité pédagogique et fil conducteur :** Le projet sert de fil conducteur pour plusieurs séances de TP abordant l'architecture de **persistance polyglotte** (*Polyglot Persistence*). Les commentaires du fichier le confirment :
  - **PostgreSQL** : données transactionnelles (séance 1)
  - **MongoDB** : catalogue de jeux (séance 2)
  - **Redis** : classements, matchmaking, cache (séance 3)
  - **Neo4j** : graphe social et recommandation (séance 4)
- **Simplicité de configuration :** Définir dès le départ l'ensemble de la stack évite d'avoir à modifier la configuration Docker à chaque séance. Les étudiants disposent ainsi immédiatement de l'environnement complet et cohérent pour l'ensemble du module.

---

## Q2. Trois services ont une section volumes, un seul n'en a pas. Lequel, et qu'est-ce que ça implique pour ses données ?

- **Le service concerné :** Il s'agit de **Redis** (`pixelhub-redis`).
- **Implications pour ses données :**
  - Sans volume monté sur l'hôte, les données stockées dans Redis sont **entièrement volatiles**.
  - Si le conteneur est supprimé (par exemple avec `docker compose down`), toutes les données en cache ou clés stockées sont définitivement perdues.
  - Cela reste cohérent avec son rôle principal (cache, classements éphémères, données de session en mémoire), contrairement aux autres bases (Postgres, Mongo, Neo4j) dont les données doivent persister au-delà du cycle de vie des conteneurs grâce à leurs volumes nommés respectifs (`pg_data`, `mongo_data`, `neo4j_data`).

---

## Q3. Que signifie la ligne `"15432:5432"` ? Les deux nombres ne désignent pas la même chose.

La syntaxe Docker pour la redirection de ports est sous la forme `"<port_hôte>:<port_conteneur>"` :
- **`15432` (Port hôte) :** C'est le port ouvert sur votre propre machine (l'hôte). Comme indiqué en tête du fichier compose, ce port est décalé par rapport au port standard (5432) afin d'éviter tout conflit avec un service PostgreSQL qui tournerait déjà localement sur la machine physique de l'étudiant.
- **`5432` (Port conteneur) :** C'est le port d'écoute par défaut du démon PostgreSQL à l'intérieur du conteneur Docker.

**Signification :** Docker redirige les connexions arrivant sur le port `15432` de votre machine hôte vers le port `5432` du conteneur PostgreSQL.

---

## Q4. Les mots de passe sont écrits en clair dans le fichier. Est-ce acceptable ici ? Le serait-ce sur un serveur de production ?

- **Acceptable ici (en TP / développement local) :**
  - **Oui**, car il s'agit d'un environnement d'apprentissage local avec des données de test/factices, sans exposition publique sur Internet.
  - Cela simplifie la prise en main par les étudiants sans nécessiter la mise en place d'une infrastructure complexe de gestion des secrets.
- **En production :**
  - **Absolument non.** Les fichiers de configuration comme `docker-compose.yml` sont généralement archivés sur des dépôts de code (ex. Git). Laisser des identifiants en clair expose le système à des fuites de données et à des compromissions directes.
  - En production, on utilise des outils dédiés à la gestion des secrets (tels que **Docker Secrets**, **Kubernetes Secrets**, **HashiCorp Vault**, ou des variables d'environnement injectées de façon sécurisée via un coffre-fort de secrets ou un pipeline CI/CD).

---

## Q5. Les commandes ci-dessus utilisent `docker compose exec`. Qu'est-ce que ça fait exactement ? Où s'exécute la commande `redis-cli` ?

- **Rôle de `docker compose exec` :**
  - Cette commande permet d'exécuter un processus ou une commande arbitraire **à l'intérieur d'un conteneur déjà en cours d'exécution** (*running*). Docker Compose identifie le conteneur associé au nom du service (ex: `redis`, `postgres`, `mongo`) et transmet la commande au démon Docker pour exécution dans l'environnement isolé de ce conteneur.
- **Où s'exécute `redis-cli` ?**
  - La commande `redis-cli` s'exécute **à l'intérieur du conteneur `pixelhub-redis`** (dans son propre système de fichiers et son espace de nommage réseau/processus), et **non sur la machine hôte**.
  - **Avantage pratique :** Cela évite d'avoir à installer les outils d'administration ou clients en ligne de commande (`redis-cli`, `mongosh`, `pg_isready`, etc.) sur son système d'exploitation hôte : on exploite directement les binaires embarqués dans l'image Docker du service.

---

## Q6. La base de données tourne dans un conteneur Docker, et la chaîne de connexion dit `Host=localhost`. Pourquoi est-ce que ça fonctionne ?

- **Mécanisme de redirection de port (Port Binding) :**
  - Dans le fichier `docker-compose.yml`, la section `ports` de PostgreSQL déclare `"- \"${PG_PORT:-15432}:5432\""`.
  - Docker mappe ainsi le port d'écoute interne `5432` du conteneur sur le port `15432` de l'interface réseau de la **machine hôte** (`localhost`).
  - Lorsque l'API s'exécute sur votre machine et tente de se connecter à `Host=localhost;Port=15432`, elle envoie ses requêtes au port local de l'hôte, que le démon Docker intercepte et achemine fidèlement vers le port `5432` du conteneur PostgreSQL.

---

## Q7. Arrêtez l'application (Ctrl+C), relancez-la. Les trois joueurs sont-ils dupliqués ? Pourquoi ?

- **Non, les trois joueurs ne sont pas dupliqués.**
- **Pourquoi ? (Explication dans le code et la persistance) :**
  1. **Condition d'initialisation dans `Program.cs` :**
     L'insertion des trois joueurs est encapsulée dans une condition :
     ```csharp
     if (!db.Players.Any())
     {
         db.Players.AddRange(...);
         db.SaveChanges();
     }
     ```
     Avant d'insérer, EF Core exécute une requête `SELECT EXISTS(...)` sur la table `Players`. Dès lors que la table contient déjà des enregistrements, `db.Players.Any()` retourne `true` (donc `!db.Players.Any()` est `false`), et l'insertion initiale est ignorée.
  2. **Persistance des données via le volume Docker :**
     Comme PostgreSQL utilise un volume nommé (`pg_data`), les lignes insérées lors du premier démarrage restent écrites et persistées sur le disque même après l'arrêt ou le redémarrage de l'API.




---
---

# TP 2 — Le catalogue de jeux dans MongoDB

## Q1. Vous retrouvez le champ `_id`, déjà aperçu au TP 1 (étape 6.2), que personne n'a écrit dans `games.json`. Cette fois, intéressez-vous à qui l'a fabriqué : le serveur MongoDB, ou l'outil qui a envoyé les documents (`mongoimport`) ? Comparez avec l'`Id` 4 de *Pixel* au TP 1 (Q8) : qui l'avait choisi, et à quel moment le connaissiez-vous ?

- **Qui fabrique l'`_id` dans MongoDB :**
  - C'est **l'outil client** (`mongoimport` ici, ou le driver MongoDB en C#) qui génère l'`ObjectId` côté client avant de transmettre le document au serveur. La structure même d'un `ObjectId` (4 octets de timestamp, 5 octets aléatoires propres à la machine/processus, et 3 octets de compteur) a été pensée pour être générée de façon décentralisée sans risque de collision.
- **Comparaison avec l'`Id` 4 de *Pixel* (PostgreSQL / TP 1) :
  - Dans PostgreSQL, la clé primaire était générée par une séquence auto-incrémentée (`IDENTITY` / `serial`) gérée par le **serveur PostgreSQL**.
  - L'identifiant est attribué uniquement au moment où l'`INSERT` s'exécute sur le serveur. On ne le connaît donc qu'**après** l'écriture en base (via le résultat SQL ou après `SaveChanges()`). Avec MongoDB, l'identifiant peut être connu et manipulé côté client avant même l'écriture.

---

## Q2. À l'exercice 5, `plateformes` est un tableau et vous avez écrit `{ plateformes: "Switch" }`, sans opérateur particulier. Comment auriez-vous fait la même chose en SQL, avec un modèle relationnel normalisé ?

- En SQL normalisé (3NF), on ne peut pas stocker de liste multivaluée directement dans une colonne atomique (1ère Forme Normale).
- Il aurait fallu décomposer le modèle en **trois tables** :
  1. Une table `jeux (id, titre, ...)`
  2. Une table `plateformes (id, nom)`
  3. Une table associative / de jointure `jeux_plateformes (jeu_id, plateforme_id)`
- La requête aurait alors nécessité une **jointure** (`JOIN`) :
  ```sql
  SELECT j.titre
  FROM jeux j
  JOIN jeux_plateformes jp ON j.id = jp.jeu_id
  JOIN plateformes p ON jp.plateforme_id = p.id
  WHERE p.nom = 'Switch';
  ```
- **Apport de MongoDB :** Le support natif des tableaux permet d'interroger directement les éléments d'une liste sans jointure relationnelle.

---

## Q3. Vous tapez `--authenticationDatabase admin` depuis le TP 1 sans qu'on vous ait dit pourquoi. Pourquoi faut-il l'ajouter aux commandes `mongoimport` et `mongosh` ? Où se trouve le compte `pixelhub` ?

- Dans MongoDB, les comptes utilisateurs sont associés et stockés dans une base de données d'authentification spécifique.
- Le compte utilisateur `pixelhub` a été créé dans la base système **`admin`** (lors de l'initialisation du conteneur via `MONGO_INITDB_ROOT_USERNAME` dans `docker-compose.yml`).
- Par défaut, si l'on ne précise pas `--authenticationDatabase`, MongoDB cherche les identifiants dans la base cible à laquelle on se connecte (`pixelhub`), où ce compte n'existe pas, provoquant un échec d'authentification (`MongoAuthenticationException`).
- L'option `--authenticationDatabase admin` indique au serveur de vérifier les credentials contre la base `admin`.

---

## Q4. Personne ne vous a arrêté. Est-ce une bonne nouvelle ou un problème ? Argumentez en imaginant un projet à quatre développeurs, six mois plus tard.

- **Une bonne nouvelle à court terme (flexibilité & prototypage) :**
  - Très grande agilité : aucune migration DDL bloquante pour ajouter des champs.
  - Représentation naturelle de documents polymorphes ou hétérogènes (ex: un FPS stocke `joueursParEquipe` et `cartes`, un jeu de gestion stocke `dureeMoyennePartieHeures`).
- **Un problème majeur à terme en équipe :**
  - Dans un projet à quatre développeurs après six mois, l'absence de contrainte de schéma (*schema-less* par défaut) peut entraîner de graves dérives :
    - Fautes de frappe dans les clés (`nbNiveaux` vs `nombreNiveaux`) non détectées.
    - Types de données incohérents (chaîne dans un document, entier dans un autre).
    - Champs orphelins ou données abandonnées sans documentation.
  - Côté applicatif C#, la désérialisation échouera (`FormatException`) ou nécessitera un surcroît de code défensif.
  - En production, il est donc indispensable de poser des règles de validation de schéma (ex. *JSON Schema Validation* dans MongoDB) et des conventions d'équipe.

---

## Q5. Le résultat de l'exercice 9 vous surprend-il ? Que s'est-il passé exactement, et pourquoi n'y a-t-il eu aucun message d'erreur ?

- **Ce qui s'est passé :**
  - MongoDB est **sensible à la casse** : la collection `jeux` est différente de `Jeux`.
  - MongoDB utilise une approche de création à la demande (*lazy*) : une lecture `find()` sur une collection inexistante n'est pas considérée comme une erreur. MongoDB consulte la collection demandée, constate qu'elle ne contient aucun document correspondant au filtre, et renvoie un curseur vide (`[]`).
  - Aucun message d'erreur n'est émis car rechercher dans une collection vide est une opération syntaxiquement et logiquement valide pour le moteur.
  - *(Remarque : un `insertOne` sur `db.Jeux` aurait créé silencieusement une deuxième collection `Jeux` distincte).*

---

## Q6. L'exercice 16 échoue. Recopiez le message d'erreur, et expliquez pourquoi MongoDB refuse. Quelle commande utiliseriez-vous si vous vouliez réellement remplacer le document en entier ?

- **Message d'erreur obtenu :**
  ```text
  MongoInvalidArgumentError: Update document requires atomic operators
  ```
- **Pourquoi MongoDB refuse :**
  - La méthode `updateOne` est dédiée aux modifications partielles de documents et exige obligatoirement des opérateurs atomiques (tels que `$set`, `$inc`, `$push`, `$unset`).
  - Sans opérateur comme `$set`, passer `{ note: 4.4 }` risquerait d'écraser par accident l'intégralité du document (en supprimant tous ses autres champs à l'exception de l'`_id`). Le driver bloque donc l'appel par sécurité.
- **Commande pour réellement remplacer le document :**
  - Pour remplacer un document dans sa globalité, MongoDB impose d'utiliser explicitement la méthode `replaceOne` :
    ```javascript
    db.jeux.replaceOne({ titre: "Valorant" }, { titre: "Valorant", note: 4.4, genre: "FPS" })
    ```

---

## Q7. À l'exercice 12, vous avez ajouté un champ `nbVotes` à un seul document, alors que les dix autres n'en ont pas. Quelle commande SQL aurait été nécessaire pour faire l'équivalent en relationnel, et quelle en aurait été la conséquence sur les autres lignes ?

- **Commandes SQL nécessaires :**
  1. Modifier le schéma de la table pour ajouter la colonne :
     ```sql
     ALTER TABLE jeux ADD COLUMN nb_votes INTEGER;
     ```
  2. Mettre à jour la ligne ciblée :
     ```sql
     UPDATE jeux SET nb_votes = 0 WHERE titre = 'Terraria';
     ```
- **Conséquence sur les autres lignes :**
  - En modèle relationnel à schéma fixe, la nouvelle colonne est ajoutée à **toutes les lignes existantes** de la table.
  - Toutes les autres lignes reçoivent obligatoirement la valeur `NULL` (ou la valeur par défaut définie), consommant de l'espace de stockage et imposant la gestion des valeurs nulles dans l'application, même si ce champ n'a aucun sens pour d'autres jeux. En MongoDB, le champ n'existe que sur le document modifié.

---

## Q8. Lisez `IGameCatalog`. Y a-t-il un seul endroit dans cette interface où le mot « Mongo » apparaît ? Pourquoi est-ce important, à votre avis ?

- **Constat :**
  - Non, le terme « Mongo » n'apparaît à aucun endroit dans `IGameCatalog`. L'interface ne manipule que des abstractions C# et le modèle métier `Game` (`Task<List<Game>>`, `string genre`, `int count`, etc.).
- **Pourquoi c'est important :**
  - **Principe d'Inversion des Dépendances (DIP) / Clean Architecture :** Les couches supérieures (les endpoints d'API dans `Program.cs`) dépendent d'un contrat (l'interface) et non d'une implémentation concrète.
  - **Découplage technologique :** L'application ne sait pas comment ni où sont stockés les jeux. Si on décide plus tard de remplacer MongoDB par PostgreSQL, un cache Redis, ou un service mocké pour des tests unitaires, l'interface `IGameCatalog` et les endpoints restent inchangés. Seule la classe d'implémentation injectée dans le conteneur IoC est modifiée.


---

## Q9. Quelle exception obtenez-vous, et sur quel champ ? Expliquez la cause : qu'est-ce que le driver a essayé de faire, et pourquoi n'a-t-il pas su ?

- **Exception obtenue :**
  ```text
  System.FormatException: Element 'joueursParEquipe' does not match any field or property of class PixelHub.Api.Models.Game.
  ```
- **Sur quel champ :** Le champ `joueursParEquipe` (présent dans le document de *Counter-Strike 2*).
- **Cause :**
  - Le driver C# MongoDB tente de désérialiser le document BSON retourné par MongoDB vers une instance de la classe `Game`.
  - Par défaut, le sérialiseur BSON C# applique une politique stricte : chaque champ présent dans le document BSON doit posséder une propriété ou un champ correspondant dans la classe C#.
  - Dès qu'il rencontre un champ non déclaré dans la classe `Game` (`joueursParEquipe`), le driver ne sait pas où stocker cette valeur et lève immédiatement une `FormatException` pour signaler une incohérence entre le document et le modèle.

---

## Q10. `[BsonIgnoreExtraElements]` fait disparaître l'erreur, mais au prix de quoi ? Que devient `joueursParEquipe` quand vous appelez `/games` ?

- **Au prix de quoi :**
  - Cela se fait au prix d'une **perte d'information** au niveau applicatif. Tout champ présent dans MongoDB mais non déclaré dans la classe C# est silencieusement ignoré et jeté lors de la désérialisation.
- **Que devient `joueursParEquipe` :**
  - Le champ `joueursParEquipe` n'est pas chargé en mémoire dans l'objet `Game`.
  - Par conséquent, il est totalement absent du flux JSON retourné par l'endpoint `/games`.

---

## Q11. Comparez les trois approches possibles pour gérer un schéma variable en C# : `[BsonIgnoreExtraElements]`, `[BsonExtraElements]`, et une hiérarchie de classes (`FpsGame : Game`, etc.). Quel est l'avantage et l'inconvénient de chacune ?

1. **`[BsonIgnoreExtraElements]`** :
   - **Avantage :** Solution la plus simple et directe. L'application reste stable face aux variations de schéma et ne mappe que le sous-ensemble commun de champs dont elle a besoin.
   - **Inconvénient :** Perte de données : les champs spécifiques aux documents ne peuvent pas être exploités, modifiés ou réécrits par l'application.
2. **`[BsonExtraElements]` (avec un dictionnaire / BsonDocument dynamique)** :
   - **Avantage :** Conserve toutes les données non déclarées sans perte. Permet d'exposer dynamiquement les champs spécifiques dans l'API (via un dictionnaire).
   - **Inconvénient :** Perte du typage fort C# sur ces champs (manipulation de clés/valeurs génériques `object` ou `BsonDocument`), et complexité de conversion entre la sérialisation BSON et le sérialiseur JSON de l'API (`[JsonIgnore]`, `ToDictionary()`).
3. **Hiérarchie de classes (`FpsGame : Game`, `GestionGame : Game`, avec discriminateur BSON)** :
   - **Avantage :** Typage fort complet, propreté du POCO, validation à la compilation et autocomplétion pour chaque famille de jeu.
   - **Inconvénient :** Rigidité accrue : chaque nouvelle variante nécessite de coder et maintenir une nouvelle classe C#. Moins adapté si les documents évoluent continuellement ou sont très imprévisibles.

---

## Q12. Écrivez la requête SQL qui produirait le même résultat sur une table `jeux(titre, genre, note)`. Comparez les deux. Est-ce ici que MongoDB apporte quelque chose par rapport à PostgreSQL ? Si non, où l'apport se situe-t-il dans ce TP ?

- **Requête SQL équivalente :**
  ```sql
  SELECT genre, AVG(note) AS "NoteMoyenne", COUNT(*) AS "Nombre"
  FROM jeux
  GROUP BY genre
  ORDER BY "NoteMoyenne" DESC;
  ```
- **Comparaison :**
  - La requête SQL est plus lisible, déclarative et naturelle pour effectuer des calculs statistiques groupés (`GROUP BY`, `AVG`, `COUNT`).
  - Le pipeline d'agrégation MongoDB en C# (`Aggregate().Group(...).SortByDescending(...)`) est plus impératif et verbeux.
- **MongoDB apporte-t-il quelque chose ici ?**
  - **Non**, sur les agrégations tabulaires classiques, MongoDB n'apporte rien de supérieur à PostgreSQL, qui dispose d'un optimiseur de requêtes relationnelles extrêmement performant et éprouvé.
  - **Où se situe le réel apport de MongoDB dans ce TP :**
    1. **Le polymorphisme / schéma flexible :** Pouvoir stocker des jeux aux attributs très disparates dans une même collection sans colonnes `NULL` superflues.
    2. **Les types complexes et collections imbriquées :** La gestion native des tableaux (`plateformes`, `tags`, `cartes`) sans tables de jointure n:m.

---

## Question orale de l'enseignant : *« Les avis d'un jeu : tu imbriques ou tu référencies ? »*

- **Réponse argumentée :**
  - **On imbrique (Embedding) si :**
    - Le nombre d'avis reste modéré et borné (pour ne pas risquer d'atteindre la limite technique de 16 Mo par document BSON).
    - Les avis sont quasi systématiquement affichés en même temps que la fiche détaillée du jeu (lecture conjointe fréquente en 1 seule opération d'E/S).
  - **On référence (Referencing avec une collection `avis` distincte) si :**
    - Le volume d'avis peut exploser (des milliers d'avis par jeu), ce qui alourdirait le document `Game` lors des requêtes simples (comme lister les jeux du catalogue).
    - Les avis ont leur propre cycle de vie (pagination, votes sur les avis, modération indépendante, analyse statistique globale des utilisateurs).
  - **Conclusion :** Dans un système réaliste comme PixelHub avec une communauté active, le **référencement** (ou une approche hybride avec les derniers avis imbriqués et l'historique archivé/référencé) est généralement préférable.


---
---

# TP 2 — Pour aller plus loin (Bonus)

## 1. Index et mesure (`explain("executionStats")`)

- **Avant la création de l'index :**
  - Requête : `db.jeux.find({ genre: "FPS" }).explain("executionStats")`
  - Étape d'exécution : `COLLSCAN` (Collection Scan, parcours séquentiel complet).
  - Documents examinés : `totalDocsExamined = 11`.
  - Documents retournés : `nReturned = 2`.
  - Temps d'exécution : ~21 ms.
- **Création de l'index :**
  ```javascript
  db.jeux.createIndex({ genre: 1 })
  ```
- **Après la création de l'index :**
  - Étape d'exécution : `IXSCAN` (Index Scan) suivi de `FETCH`.
  - Clés d'index examinées : `totalKeysExamined = 2`.
  - Documents examinés : `totalDocsExamined = 2`.
  - Documents retournés : `nReturned = 2`.
  - Temps d'exécution : ~8 ms.
- **Constat sur 10 documents et explication :**
  - Sur un jeu de données aussi réduit (10-11 documents), le gain réel est imperceptible (le parcours complet en RAM est instantané). L'index introduit même une structure mémoire supplémentaire et un léger surcoût à l'écriture.
  - En revanche, sur une base de production contenant plusieurs millions de jeux, l'index est indispensable : il transforme une recherche linéaire en $O(N)$ (qui saturerait le CPU et le disque) en une recherche logarithmique en $O(\log N)$.

---

## 2. Modéliser les avis (Documents imbriqués)

- **Ajout des avis en imbriqué :**
  ```javascript
  db.jeux.updateOne(
    { titre: "Baldur's Gate 3" },
    {
      $set: {
        avis: [
          { auteur: "Alice", note: 5, commentaire: "Chef d'œuvre absolu !" },
          { auteur: "Bob", note: 4, commentaire: "Très bon mais chronophage." }
        ]
      }
    }
  );
  ```
- **Requête renvoyant les jeux ayant au moins un avis avec la note 5 :**
  ```javascript
  db.jeux.find({ "avis.note": 5 }, { titre: 1, avis: 1, _id: 0 })
  ```
  *(Ou avec `$elemMatch` si l'on a plusieurs critères sur le même sous-document : `db.jeux.find({ avis: { $elemMatch: { note: 5 } } })`)*.
- **Résultat :** Renvoie bien *Baldur's Gate 3*. Grâce au modèle orienté document, on filtre directement à l'intérieur d'un tableau d'objets imbriqués sans jointure.

---

## 3. Validation de schéma (`$jsonSchema`)

- **Commande pour imposer un champ `titre` de type chaîne obligatoire :**
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
            description: "doit être une chaîne de caractères et est obligatoire"
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
- **Résultat observé :**
  - L'insertion est rejetée avec l'erreur `MongoServerError: Document failed validation`.
  - Détail de l'erreur : `missingProperties: [ 'titre' ]` sur la règle `$jsonSchema`.
  - Cela permet de sécuriser le schéma au niveau du moteur tout en conservant la souplesse de MongoDB sur les autres attributs.

---

## Question d'ouverture pour la séance 3

> *« PixelHub doit afficher le nombre de joueurs actuellement en ligne, mis à jour chaque seconde. Vous le stockez dans PostgreSQL, ou dans MongoDB ? »*

- **Réponse :** Dans **aucun des deux** !
- **Moteur adapté :** On utilise **Redis** (qui sera vu à la séance 3).
- **Pourquoi :**
  - Écrire et mettre à jour un compteur chaque seconde dans PostgreSQL ou MongoDB entraînerait des écritures disque et des verrous transactionnels inutiles pour une donnée très éphémère.
  - Redis stocke toutes ses données en mémoire vive (RAM) et propose des opérations atomiques instantanées en $O(1)$ (comme `INCR` / `DECR`), ce qui en fait le moteur idéal pour les compteurs temps réel et les données de présence.

---
---

# TP 3 — Classements, matchmaking et cache avec Redis

## Partie 1 — Les structures dans `redis-cli`

### Q1. `INCR` est une opération atomique. Qu'est-ce que ça veut dire concrètement, et pourquoi est-ce important si mille joueurs terminent une partie en même temps ? Comment auriez-vous fait la même chose en SQL, et quelle différence cela fait-il ?

- **Signification concrète de l'atomicité :**
  - Une opération est atomique lorsqu'elle s'exécute en une étape indivisible et ininterruptible. Aucun autre client ne peut observer ou intercaler une opération sur un état intermédiaire.
- **Importance face à 1 000 joueurs simultanés :**
  - Redis traite les commandes de manière séquentielle sur une boucle d'événements monothreadée (*single-threaded event loop*).
  - 1 000 requêtes `INCR` arrivant en même temps sont traitées les unes après les autres sans aucune condition de concurrence (*race condition*). Le compteur passera de manière garantie et exacte de $N$ à $N + 1000$.
- **Équivalent en SQL :**
  - En SQL, on écrirait :
    ```sql
    UPDATE parties SET compteur = compteur + 1 WHERE id = 1;
    ```
- **Différence majeure :**
  - En SQL relationnel, cette opération pose un **verrou exclusif de ligne** (*Row Lock*), écrit dans le journal des transactions (*WAL*) et provoque des écritures disque. Si 1 000 requêtes concurrentes frappent cette même ligne, elles s'empilent en attente du verrou, écroulant le débit du serveur.
  - Dans Redis, l'opération s'effectue purement en mémoire vive (RAM) en quelques nanosecondes sans verrou transactionnel lourd.

---

### Q2. On aurait pu sérialiser tout le joueur en JSON dans une simple `String`. Décrivez les étapes qu'il aurait alors fallu faire pour ajouter 250 XP, et le problème qui apparaît si deux requêtes le font en même temps.

- **Étapes nécessaires avec du JSON dans une `String` :**
  1. Lire la chaîne JSON complète depuis Redis via le réseau : `GET joueur:7`.
  2. Désérialiser la chaîne JSON en objet applicatif (C# / POCO).
  3. Modifier la propriété en mémoire : `joueur.Xp += 250`.
  4. Resérialiser l'objet complet en JSON.
  5. Réécrire toute la chaîne dans Redis : `SET joueur:7 "..."`.
- **Problème de concurrence (*Lost Update / Race Condition*) :**
  - Si deux requêtes concurrentes $A$ et $B$ tentent d'ajouter 250 XP simultanément :
    - $A$ lit le JSON (`xp: 3400`).
    - $B$ lit le JSON avant que $A$ n'ait sauvegardé (`xp: 3400`).
    - $A$ calcule 3650 et fait son `SET`.
    - $B$ calcule 3650 et fait son `SET`.
    - **Résultat :** Le joueur a 3650 XP au lieu de 3900 XP. La mise à jour de $A$ a été écrasée et perdue.
- **Avantage du `Hash` :**
  - Avec `HINCRBY joueur:7 xp 250`, l'opération est exécutée directement et atomiquement sur le champ cible par le moteur Redis, sans nécessiter de rapatriement réseau ni de désérialisation de l'objet complet.

---

### Q3. Les deux dernières commandes (`ZREVRANGE` et `ZRANGE`) ne renvoient pas la même chose. Expliquez la différence, et dites laquelle vous utiliserez pour afficher un « top 3 ».

- **Différence :**
  - Dans un Sorted Set (`ZSet`), les membres sont ordonnés par défaut selon leur score de manière **croissante** (du plus petit au plus grand score).
  - `ZRANGE classement:global 0 2 WITHSCORES` parcourt du début vers la fin (ordre croissant) et renvoie les 3 moins bons scores : `Pixel (1800)`, `Krayz (3200)`, `Nova (4500)`.
  - `ZREVRANGE classement:global 0 2 WITHSCORES` (ou `ZRANGE ... REV`) inverse le sens de parcours et renvoie les éléments par ordre **décroissant** : `Ombre (5100)`, `Nova (4500)`, `Krayz (3200)`.
- **Pour afficher un « top 3 » :**
  - On utilise obligatoirement **`ZREVRANGE`** (ou `ZRANGE REV`), car un classement valorise les joueurs possédant les scores les plus élevés aux premières positions.

---

### Q4. `ZREVRANK` renvoie un nombre. Si vous affichez ce nombre tel quel au joueur, que va-t-il lire ? Que faut-il faire ?

- **Ce que lirait le joueur :**
  - `ZREVRANK` renvoie un index informatique basé sur zéro (*0-based index*).
  - Si le joueur est le meilleur (comme `Krayz` après avoir reçu 2000 points avec son score de 5200), la commande renvoie `0`.
  - Affiché tel quel, le joueur lirait qu'il est **« 0e »**, ce qui est anormal pour un être humain.
- **Ce qu'il faut faire :**
  - Il faut systématiquement **ajouter 1** à la valeur retournée (`rang = index + 1`) dans la couche applicative C# avant de l'exposer ou de l'afficher.

---

### Q5. Qui sort de la file au `RPOP`, et pourquoi ? Qu'aurait donné `LPOP` à la place ?

- **Qui sort au `RPOP` :**
  - C'est **"Nova"** qui sort.
- **Pourquoi :**
  - Les joueurs ont été ajoutés par la gauche avec `LPUSH` dans l'ordre : `"Nova"`, puis `"Krayz"`, puis `"Ombre"`.
  - L'état interne de la liste est donc `["Ombre", "Krayz", "Nova"]`.
  - `RPOP` dépile par la droite (la queue de la liste). Nova étant entrée la première, elle est la première à sortir. Cela respecte le principe **FIFO** (*First-In, First-Out*), fondamental pour une file d'attente de matchmaking équitable.
- **Ce qu'aurait donné `LPOP` :**
  - `LPOP` aurait dépilé par la gauche (la tête de la liste), extrayant **"Ombre"** (le dernier joueur entré). Cela aurait formé une pile **LIFO** (*Last-In, First-Out*), pénalisant les joueurs ayant attendu le plus longtemps.

---

### Q6. Quelles valeurs obtenez-vous ? Que renvoie `TTL` sur une clé sans expiration (testez avec `compteur:parties`) ? Que signifie chacune des valeurs de retour ?

- **Valeurs obtenues :**
  - Immédiatement après création avec `EX 30` : `TTL enligne:Nova` renvoie un entier positif (ex. `30` ou `28`), et `EXISTS` renvoie `1`.
  - Après écoulement des 30 secondes : `TTL enligne:Nova` renvoie **`-2`**, et `EXISTS` renvoie **`0`**.
  - Sur une clé sans expiration (`compteur:parties`) : `TTL` renvoie **`-1`**.
- **Signification des trois valeurs de retour de `TTL` :**
  - **Entier positif ($> 0$) :** Nombre de secondes restantes avant la destruction programmée de la clé.
  - **`-1` :** La clé existe bien en mémoire, mais **aucune durée de vie ne lui est associée** (clé persistante).
  - **`-2` :** La clé **n'existe pas** (ou a expiré et a été purgée de la mémoire).

---

### Q7. Personne n'a lancé de tâche de nettoyage. Quel code auriez-vous dû écrire pour obtenir le même comportement avec PostgreSQL, et quels risques ce code introduit-il ?

- **Code nécessaire dans PostgreSQL :**
  1. Ajouter une colonne horodatée sur la table des sessions : `derniere_activite TIMESTAMP`.
  2. Développer et héberger un processus d'arrière-plan externe (cron OS, `BackgroundService` .NET, ou extension SQL `pg_cron`) exécutant à intervalle régulier :
     ```sql
     DELETE FROM connexions WHERE derniere_activite < NOW() - INTERVAL '30 seconds';
     ```
- **Risques introduits par cette approche :**
  1. **Contention et surcharge disque :** Des `DELETE` fréquents génèrent des verrous, polluent le WAL et créent des lignes mortes (*dead tuples*), obligeant à des `VACUUM` fréquents.
  2. **Vulnérabilité aux pannes (*Silent Failure*) :** Si le processus de nettoyage externe plante ou est mal planifié, les lignes s'accumulent silencieusement et saturent l'espace disque.
  3. **Incohérence temporelle :** Entre deux cycles du nettoyeur, des données théoriquement expirées restent visibles et faussent les comptages si chaque requête n'ajoute pas un filtre `WHERE derniere_activite >= ...`.
  - **Avantage Redis :** Le TTL est géré de façon native et autonome au cœur du moteur (nettoyage passif à l'accès + nettoyage actif probabiliste).

---
---

## Partie 2 — Redis dans PixelHub

### Q8. Ce service est enregistré en singleton (`builder.Services.AddSingleton<IConnectionMultiplexer>`). Cherchez dans la documentation de StackExchange.Redis ce qu'est un `ConnectionMultiplexer` et ce qui se passerait si on en créait un à chaque requête HTTP. Pourquoi ce bug serait-il invisible aujourd'hui en TP ?

- **Qu'est-ce qu'un `ConnectionMultiplexer` :**
  - C'est l'objet pivot de la bibliothèque `StackExchange.Redis`. Il encapsule et gère la connexion réseau sous-jacente au serveur Redis.
  - Il a été spécifiquement conçu pour être **thread-safe** et partagé : il implémente un multiplexage de sockets TCP, permettant à de multiples threads concurrents d'acheminer leurs commandes asynchrones à travers une même connexion persistante hautement optimisée.
- **Ce qui se passerait si on en créait un à chaque requête HTTP (ex. en Transient ou Scoped) :**
  - Chaque requête HTTP forcerait l'ouverture d'un nouveau socket TCP, le handshake réseau et la négociation TLS/auth.
  - En refermant la connexion, les sockets entreraient en état d'attente prolongée `TIME_WAIT` au niveau du noyau de l'OS.
  - Sous trafic réel, cela mènerait immédiatement à l'**épuisement des sockets/ports locaux** (*Socket / Port Exhaustion*), provoquant des erreurs `SocketException: No buffer space available` ou `RedisTimeoutException`, une fuite mémoire et un effondrement des performances.
- **Pourquoi ce bug serait invisible aujourd'hui en TP :**
  - En environnement local de TP, nous n'exécutons que quelques requêtes manuelles séquentielles (quelques dizaines tout au plus).
  - Le système d'exploitation dispose de milliers de ports éphémères libres et n'est soumis à aucune concurrence massive. Le bug d'épuisement de sockets ne se manifeste qu'à partir d'un trafic soutenu de plusieurs dizaines ou centaines de requêtes par seconde.

---

### Q9. Supprimez `Order.Descending` des deux méthodes, relancez, et regardez `/leaderboard/top/5`. Le résultat est-il visiblement faux ? Pourquoi ce bug est-il particulièrement dangereux ?

- **Résultat observé :**
  - En omettant `Order.Descending`, Redis applique son ordre naturel par défaut qui est **croissant** (les scores les plus bas en tête).
  - L'API retourne les 5 joueurs ayant les plus faibles scores (ex. `Pixel` avec 1800 points en premier).
- **Le résultat est-il visiblement faux ?**
  - **Non**, au premier regard le résultat paraît parfaitement valide : l'API répond avec un code HTTP 200, un tableau JSON bien formé de 5 joueurs avec leurs scores et des rangs 1, 2, 3, 4, 5. Aucun message d'erreur ni exception n'est levé.
- **Pourquoi ce bug est particulièrement dangereux :**
  - Il s'agit d'un bug **sémantique / logique silencieux**. Tout le pipeline technique fonctionne, mais le classement est inversé : le « Top 5 » devient un « Flop 5 ».
  - Dans un système de jeu en production, une telle inversion distribuerait indûment les récompenses de fin de saison, fausserait l'algorithme de matchmaking et détruirait l'intégrité compétitive sans qu'aucune sonde de monitoring technique (alertes d'erreur 500, crash logs) ne s'allume.

---

### Q10. Comment un joueur sort-il de cet ensemble (`Set`) quand il ferme son navigateur sans se déconnecter ? Cherchez la commande Redis qui ferait expirer un seul membre d'un Set. Que trouvez-vous ?

- **Sortie d'un `Set` en cas de fermeture brutale du navigateur :**
  - Le joueur **ne sort jamais** de l'ensemble. Il y reste présent indéfiniment car aucune requête HTTP de déconnexion n'est émise.
- **Commande Redis pour faire expirer un seul membre d'un Set :**
  - Il n'existe **absolument aucune commande** dans Redis permettant d'affecter une durée de vie (TTL) à un membre individuel d'un `Set` (ni d'ailleurs à un champ individuel d'un `Hash` ou à un élément d'une `List`).
  - La commande `EXPIRE` s'applique strictement et exclusivement à **la clé complète** dans Redis.
  - Par conséquent, la structure `Set` standard est incapable de modéliser un système de présence dynamique avec expiration sans intervention manuelle.

---

### Q11. Décrivez votre solution avant de la coder : quelle structure, quelle clé, que met-on dans le score, et quelles commandes pour compter et pour purger ?

- **Structure choisie :** Le **Sorted Set** (`ZSet`).
- **Nom de la clé :** `enligne:joueurs`.
- **Membre :** Le pseudo du joueur (`pseudo`).
- **Valeur du Score :** Le **timestamp Epoch Unix en secondes** (`DateTimeOffset.UtcNow.ToUnixTimeSeconds()`) correspondant à l'horodatage de sa dernière activité (*heartbeat*).
- **Commandes utilisées :**
  1. **Enregistrer un signe de vie (`Touch`) :**
     - Commande : `ZADD enligne:joueurs <timestamp_actuel> <pseudo>`
     - Méthode C# : `_db.SortedSetAddAsync("enligne:joueurs", pseudo, now)`
     - *Effet :* Ajoute le joueur s'il est nouveau, ou met à jour son score (son dernier horodatage) s'il est déjà présent.
  2. **Purger les joueurs inactifs :**
     - Commande : `ZREMRANGEBYSCORE enligne:joueurs 0 (<timestamp_actuel> - 60)`
     - Méthode C# : `_db.SortedSetRemoveRangeByScoreAsync("enligne:joueurs", 0, now - 60)`
     - *Effet :* Supprime en une seule opération $O(\log N + M)$ tous les membres dont le dernier signe de vie date de plus de 60 secondes.
  3. **Compter les joueurs en ligne :**
     - Commande : `ZCARD enligne:joueurs`
     - Méthode C# : `_db.SortedSetLengthAsync("enligne:joueurs")`
     - *Effet :* Retourne instantanément la cardinalité de l'ensemble après la purge.

---

### Q12. Combien de lignes avez-vous modifiées dans vos endpoints pour ajouter ce cache ? Pourquoi ? Quel choix de la séance 2 rend cela possible ?

- **Nombre de lignes modifiées dans les endpoints :** **0 ligne** (strictement aucune).
- **Pourquoi :**
  - Grâce au respect du **Principe d'Inversion des Dépendances (DIP)** et à l'application du **Pattern Decorator**.
  - Les endpoints minimalistes de `Program.cs` (`app.MapGet("/games", async (IGameCatalog catalog) => ...)`) ne dépendent que de l'interface abstraite `IGameCatalog`.
  - La classe `CachedGameCatalog` implémente ce même contrat `IGameCatalog` et prend en dépendance l'implémentation originale `MongoGameCatalog`. Lors de l'enregistrement dans le conteneur IoC :
    ```csharp
    builder.Services.AddSingleton<MongoGameCatalog>();
    builder.Services.AddSingleton<IGameCatalog>(sp =>
        new CachedGameCatalog(
            sp.GetRequiredService<MongoGameCatalog>(),
            sp.GetRequiredService<IConnectionMultiplexer>()));
    ```
    L'injection de dépendances substitue de façon transparente l'instance concrète par le proxy de cache sans toucher au code des endpoints.
- **Choix de la séance 2 rendant cela possible :**
  - L'introduction dès le TP 2 de l'interface `IGameCatalog` pour abstraire l'accès aux données MongoDB, plutôt que d'injecter directement `IMongoDatabase` ou une classe concrète dans les handlers HTTP.

---
---

## Partie 3 — Mesurer, et se méfier de la mesure

### Q13. Relevez vos mesures. Le gain est-il spectaculaire ? Quelle conclusion honnête pouvez-vous en tirer, et dans quelles conditions un cache devient-il réellement décisif ?

- **Mesures relevées sur `/games/timed` :**
  - **1er appel (Cache Miss — lecture MongoDB + écriture Redis) :** **~5.54 ms**
  - **2e appel (Cache Hit — lecture directe RAM Redis) :** **~0.82 ms**
  - **3e appel (Cache Hit) :** **~0.58 ms**
  - **4e et 5e appels (Cache Hit) :** **~0.37 ms**
- **Le gain est-il spectaculaire ?**
  - En valeur relative : **Oui**, le temps de réponse est divisé par $\approx 15$ (gain de 93% du temps de traitement).
  - En valeur absolue : **Non**, passer de 5,5 ms à 0,4 ms est totalement imperceptible pour un être humain sur un écran.
- **Conclusion honnête :**
  - Sur un petit volume local (11 documents MongoDB stockés sur un SSD local), MongoDB est déjà si véloce que l'ajout d'une couche de cache Redis n'apporte aucun bénéfice tangible pour l'utilisateur final.
- **Conditions où le cache devient réellement décisif :**
  1. **Requêtes lourdes et agrégations complexes :** Requêtes analytiques balayant des millions de documents ou réalisant des calculs d'agrégation groupés qui prennent plusieurs centaines de millisecondes ou secondes en base.
  2. **Trafic massif et protection de la base (Effet bouclier) :** Sous une charge de 10 000 requêtes/seconde, MongoDB saturerait ses connexions et ses entrées/sorties disque. Redis absorbe 99% du trafic en RAM sans solliciter le moteur de persistance.
  3. **Latence réseau inter-serveurs :** Lorsque l'application et la base de données sont sur des machines ou des régions cloud distinctes (le cache local ou in-memory élimine le RTT réseau vers la base distante).

---

### Q14. Un administrateur corrige le titre d'un jeu directement dans MongoDB. Combien de temps les joueurs verront-ils l'ancien titre, et pourquoi ? Proposez deux stratégies pour réduire ce délai, avec leur inconvénient.

- **Durée de visibilité de l'ancien titre :** Jusqu'à **60 secondes** (la durée du TTL paramétré dans `CachedGameCatalog`).
- **Pourquoi :**
  - Dans le motif *Cache-Aside*, l'API interroge Redis en premier. Tant que la clé `cache:catalogue:tous` est présente en RAM, Redis retourne le JSON préalablement mis en cache sans jamais interroger MongoDB. La modification en base reste donc invisible jusqu'à ce que la clé expire d'elle-même.
- **Stratégie 1 : Invalidation active à l'écriture (*Cache Eviction*)**
  - *Principe :* Chaque fois qu'un endpoint de modification ou d'ajout de jeu est appelé (`PUT/POST/DELETE /games`), l'API supprime explicitement la clé de cache (`await _cache.KeyDeleteAsync("cache:catalogue:tous")`). Le prochain appel sera un *cache miss* et rechargera la donnée à jour depuis MongoDB.
  - *Inconvénient :* Si la modification est effectuée **directement en base** par un administrateur (via `mongosh`, Compass ou un script SQL/BSON sans passer par l'API), le cache ne le saura jamais et restera désynchronisé jusqu'au TTL. De plus, cela couple la logique d'écriture avec la gestion du cache.
- **Stratégie 2 : TTL dynamique plus court ou Change Streams (CDC)**
  - *Option A (TTL plus court) :* Réduire le TTL à 5 secondes. *Inconvénient :* Augmente considérablement le taux de *cache miss* et sollicite davantage MongoDB.
  - *Option B (Change Streams) :* Configurer un listener d'événements MongoDB (*Change Streams*) dans l'API C# qui écoute les modifications au niveau de l'Oplog MongoDB en temps réel et purge automatiquement la clé Redis correspondante. *Inconvénient :* Architecture complexe à maintenir, consomme des ressources de streaming et nécessite un cluster / Replica Set MongoDB actif.

---

### Question orale de l'enseignant : *« Le solde de monnaie virtuelle d'un joueur : tu le mettrais dans Redis ? »*

- **Réponse argumentée :**
  - **Absolument NON.**
  - Le solde de monnaie virtuelle d'un joueur représente un actif critique (valeur monétaire ou contractuelle). Il exige des garanties strictes de **durabilité (ACID)** : chaque transaction doit être validée, protégée contre les écritures concurrentes et immédiatement écrite sur disque dans un journal des transactions (*Write-Ahead Log*) pour survivre à n'importe quel crash matériel ou coupure d'alimentation.
  - Dans notre infrastructure polyglotte, **Redis est configuré sans volume Docker de persistance** : toutes ses données résident en RAM volatile. Si le conteneur Redis redémarre ou subit un dépassement de mémoire (OOM), l'intégralité des données en cache est perdue.
  - Par conséquent, le solde doit impérativement être géré par **PostgreSQL**.
  - Redis est réservé aux données éphémères (présence en ligne), reconstructibles (classements dérivés des parties, cache catalogue) ou dont la perte n'entraîne aucun préjudice d'intégrité.

---
---

# TP 4 bis — Neo4j, puis PixelHub à quatre moteurs

## Partie 1 — Neo4j

### Q1. Le script utilise `MERGE` et non `CREATE`. Que se passerait-il si vous le lanciez deux fois avec `CREATE` ? À quoi servent les trois `CREATE CONSTRAINT` du début ?

- **Différence entre `MERGE` et `CREATE` :**
  - **`MERGE` (Match or Create) :** Vérifie d'abord si le pattern (nœud, label, propriété, relation) existe déjà dans le graphe. S'il existe, il le sélectionne sans le recréer ; s'il n'existe pas, il le crée. Cela garantit l'**idempotence** du script (on peut l'exécuter plusieurs fois sans provoquer d'effets de bord, d'erreurs ou de duplications).
  - **`CREATE` :** Crée inconditionnellement de nouveaux nœuds ou relations à chaque exécution.
- **Que se passerait-il si on le lançait deux fois avec `CREATE` ?**
  - **Avec les contraintes d'unicité en place :** Dès le second lancement, la commande `CREATE` tenterait de recréer les nœuds déjà existants (ex: `(:Joueur {pseudo: 'Nova'})`). Le moteur Neo4j lèverait immédiatement une exception de violation de contrainte d'unicité (`ConstraintValidationFailed` / `Neo.ClientError.Schema.ConstraintValidationFailed`) et interromprait la transaction.
  - **Sans ces contraintes :** Neo4j dupliquerait aveuglément l'intégralité du graphe. On obtiendrait 44 nœuds (deux nœuds "Nova", deux "Krayz", etc.) et le double de relations, corrompant la topologie du graphe.
- **À quoi servent les trois `CREATE CONSTRAINT` du début ?**
  1. **Garantir l'intégrité métier (Unicité) :** Elles empêchent physiquement la création de doublons sur les identifiants clés du domaine (`j.pseudo` pour les `Joueur`, `j.titre` pour les `Jeu`, `g.nom` pour les `Guilde`).
  2. **Performance et indexation automatique (*Schema Indexes*) :** Dans Neo4j, déclarer une contrainte d'unicité (`REQUIRE ... IS UNIQUE`) crée automatiquement un **index de schéma sous-jacent**. Cet index permet au moteur de résoudre les `MERGE` et les `MATCH` sur ces propriétés en temps constant ou logarithmique ($O(1)$ / $O(\log N)$) au lieu d'effectuer un balayage complet et coûteux de tous les nœuds (*Node By Label Scan* en $O(N)$). C'est indispensable pour maintenir de bonnes performances à grande échelle.

---

### Q2. Combien de résultats pour chacune ? Ouvrez le script d'import et expliquez l'écart. Laquelle est correcte, et pourquoi aucune erreur ne vous a prévenu ?

- **Nombre de résultats obtenus :**
  - **SANS flèche (`-[:AMI_DE]-`) :** **2 résultats** (`Nova`, `Pixel`).
  - **AVEC flèche (`-[:AMI_DE]->`) :** **1 seul résultat** (`Pixel`).
- **Explication de l'écart dans le script d'import (`graph-import.cypher`) :**
  - Dans Neo4j, toute relation physique stockée sur disque possède obligatoirement une orientation (un nœud source et un nœud cible).
  - Pour modéliser une amitié non orientée sans dupliquer inutilement les arêtes en base, le script d'import crée une seule relation arbitrairement orientée : `MERGE (a)-[:AMI_DE]->(b)`.
  - Dans la liste d'amitiés du script :
    - On a la paire `['Nova', 'Krayz']` qui crée `(Nova)-[:AMI_DE]->(Krayz)` : pour Krayz, c'est une relation **entrante** (`<-`).
    - On a la paire `['Krayz', 'Pixel']` qui crée `(Krayz)-[:AMI_DE]->(Pixel)` : pour Krayz, c'est une relation **sortante** (`->`).
  - La requête avec flèche sortante `->` ne filtre que les relations où Krayz est la source, ignorant complètement la relation d'amitié issue de Nova.
- **Laquelle est correcte :**
  - La version **SANS flèche (`-[:AMI_DE]-`)** est la seule correcte.
  - Métier : L'amitié est une relation **symétrique et bidirectionnelle** (si Nova est amie avec Krayz, alors Krayz est ami avec Nova). Interroger le graphe sans flèche indique à Cypher de traverser le lien quel que soit son sens physique d'enregistrement.
- **Pourquoi aucune erreur ne vous a prévenu :**
  - **Validité syntaxique et sémantique pour le moteur :** Spécifier une direction avec `->` est une clause de filtrage parfaitement légitime en Cypher. Le moteur l'exécute fidèlement : il recherche les chemins vérifiant strictement cette direction.
  - Neo4j n'a aucune connaissance des règles métier humaines : il ignore si `AMI_DE` a vocation à être symétrique ou asymétrique (comme un abonnement unilatéral sur Twitter/Instagram).
  - Pour Cypher, renvoyer 1 résultat au lieu de 2 n'est ni une erreur ni une exception, mais simplement le résultat exact correspondant au prédicat restrictif demandé. C'est un **bug logique silencieux**.

---

### Q3. Remplacez `count(DISTINCT ami)` par `count(*)` : que compte chacune des deux versions ? Passez ensuite de `*1..2` à `*1..4` : combien de lignes changez-vous, et combien de CTE auriez-vous ajoutées en SQL ?

- **Différence entre `count(DISTINCT ami)` et `count(*)` :**
  - **`count(DISTINCT ami)` :** Compte le nombre de **joueurs uniques** (amis ou amis d'amis) qui possèdent le jeu. Même si un joueur est atteignable par plusieurs chemins distincts dans le réseau social, il n'est comptabilisé qu'une seule fois. C'est la métrique métier recherchée (*« Combien de mes amis possèdent ce jeu ? »*).
  - **`count(*)` :** Compte le nombre total de **chemins de graphe satisfaisant le motif**. Si un même ami $B$ possède le jeu et est ami avec deux amis directs de Nova ($A_1$ et $A_2$), il existe deux chemins menant à ce jeu (`Nova - A1 - B - Jeu` et `Nova - A2 - B - Jeu`). `count(*)` comptera alors $2$, faussant le nombre réel d'individus qui recommandent le jeu.
- **Passage de `*1..2` à `*1..4` (profondeur 4) :**
  - **En Cypher :** On ne modifie **aucune nouvelle ligne** : on change simplement un seul caractère (le chiffre `2` devient `4` dans `-[:AMI_DE*1..4]-`).
  - **En SQL classique (sans récursivité) :** Il aurait fallu ajouter **2 CTE complètes supplémentaires** (une CTE `amis_niveau3` avec auto-jointure sur le niveau 2, et une CTE `amis_niveau4` avec auto-jointure sur le niveau 3), mettre à jour la CTE d'union avec ces nouveaux niveaux, et adapter les conditions d'exclusion. En tout, cela représenterait une quinzaine de lignes SQL très verbeuses et sujettes aux erreurs (ou une complexe CTE récursive `WITH RECURSIVE` difficile à débugger).

---

### Q4. Pourquoi *Stardew Valley* n'apparaît-il jamais dans les recommandations de Nova ?

- **Raison directe dans la base de données :**
  - Dans le script d'initialisation (`graph-import.cypher`), la relation de possession existe déjà : `(:Joueur {pseudo:'Nova'})-[:POSSEDE]->(:Jeu {titre:'Stardew Valley'})`.
  - Nova possède déjà ce jeu dans sa bibliothèque personnelle.
- **Clause de filtrage dans la requête Cypher :**
  - La requête contient la condition d'exclusion : `AND NOT (moi)-[:POSSEDE]->(j)`.
  - Cette clause élimine explicitement tous les jeux déjà détenus par le joueur demandeur, ce qui est logique : un algorithme de recommandation de boutique ne doit pas recommander à un utilisateur un jeu qu'il a déjà acheté.



---

### Q5. Le Browser est sur le port 17474, et l'application utilise 17687. Pourquoi deux ports pour le même serveur ?

- **Port 17474 (redirigé vers 7474 du conteneur) — Interface Web HTTP :**
  - C'est le port d'écoute du serveur web HTTP intégré à Neo4j.
  - Il distribue l'interface graphique d'administration (**Neo4j Browser**) accessible depuis un navigateur web. Ce protocole textuel HTTP/REST n'est pas optimisé pour des flux transactionnels applicatifs massifs.
- **Port 17687 (redirigé vers 7687 du conteneur) — Protocole Bolt (`bolt://` / `neo4j://`) :**
  - C'est le port dédié au protocole réseau **Bolt**, le protocole binaire natif et propriétaire de Neo4j.
  - Bolt est un protocole avec état (*stateful*), multiplexé, compact et extrêmement performant, spécialement conçu pour les communications rapides entre les pilotes applicatifs (drivers .NET, Java, Python) et le moteur de base de données.
- **En résumé :** On sépare strictement le port de gestion/visualisation humaine (HTTP) du port d'échange binaire haute performance réservé aux applications (Bolt).

---

### Q6. Le pseudo est passé en paramètre (`$pseudo` + `new { pseudo }`), mais la profondeur est interpolée dans la chaîne. Pourquoi ? Quelle précaution le code prend-il, et que risquerait-on sans elle ?

- **Pourquoi la profondeur est interpolée et non paramétrée :**
  - Dans la grammaire syntaxique de Cypher, les quantificateurs de longueur variable (comme `*1..2` ou `*..4`) font partie intégrante de la structure même du motif (*AST* de la requête) et non des données d'exécution.
  - Le compilateur Cypher interdit l'usage de paramètres d'exécution pour la cardinalité des relations : écrire `*1..$profondeur` provoque une erreur de syntaxe immédiate. Il est donc obligatoire de l'interpoler directement dans la chaîne avant transmission au moteur.
- **Précaution prise par le code :**
  - Un filtrage strict par liste blanche / validation d'intervalle (*range check*) est exécuté avant toute interpolation :
    ```csharp
    if (profondeur < 1 || profondeur > 4)
        throw new ArgumentOutOfRangeException(nameof(profondeur), "Profondeur attendue entre 1 et 4.");
    ```
- **Ce que l'on risquerait sans cette précaution :**
  1. **Injections Cypher (*Cypher Injection*) :** Si une chaîne arbitraire pouvait être concaténée sans contrôle, un utilisateur malveillant pourrait briser la clause de pattern et exécuter des commandes destructrices (`DELETE`, `DETACH DELETE`, etc.).
  2. **Déni de service par explosion combinatoire (*Graph Traversal DoS*) :** Dans un réseau social, le nombre de chemins croît de façon exponentielle avec le nombre de sauts ($O(d^k)$). Sans limite stricte (ex. profondeur 10 ou 50), une seule requête ferait exploser l'empreinte mémoire et CPU de Neo4j, écroulant le serveur.

---

### Q7. `IDriver` en singleton, mais une session par méthode avec `await using` : pourquoi ? Quelle règle commune avec le `MongoClient` et le `ConnectionMultiplexer` ?

- **Pourquoi `IDriver` en singleton :**
  - `IDriver` est l'objet central lourd et thread-safe. Il maintient la configuration, le routage de cluster et surtout le **pool de connexions TCP physiques** vers Neo4j.
  - L'instancier à chaque requête HTTP recréerait des handshakes réseau coûteux et mènerait à l'épuisement des ports de la machine hôte.
- **Pourquoi une session par méthode avec `await using` :**
  - Une session (`IAsyncSession`) est un objet léger, **non thread-safe**, qui modélise une unité logique de travail ou un contexte de transaction unique.
  - `await using` assure que la session est proprement libérée et fermée dès la fin du bloc asynchrone, rendant immédiatement la connexion TCP sous-jacente au pool pour qu'elle puisse être réutilisée par d'autres threads.
- **Règle commune avec `MongoClient` et `ConnectionMultiplexer` :**
  - **Principe fondamental de la persistance polyglotte moderne :**
    - Les objets gérant les pools de connexions physiques (`MongoClient`, `IConnectionMultiplexer`, `IDriver`, ou `NpgsqlDataSource`) doivent toujours être enregistrés comme des **Singletons**.
    - Les sessions de travail, curseurs ou contextes de transactions (`IAsyncSession`, `DbContext`) doivent avoir une durée de vie **courte, éphémère et jetable** (*Scoped* ou encapsulés dans un `using`).
