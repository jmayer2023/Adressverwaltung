using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;



namespace Adressverwaltung
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        public MainWindow()
        {
            InitializeComponent();

            // Erstellen von Objekten für die Bearbeitung in der Liste
            Kunden = new ObservableCollection<Kunde>();
            Ansprechpartner = new ObservableCollection<Person>();

            // Zurodnen der Objekte zu den Listen
            ListeKunde.ItemsSource = Kunden;
            ListeAP.ItemsSource = Ansprechpartner;
        }


        // TODO:
        // BUG: bei Fehleingaben über den Limits der Datentypen der Tabelle kommt eine Exception, dann eine Bestätigungsmeldung, der Datensatz wird aber nicht gespeichert
        // BUG: beim speichern des gleichen Datensatzes über die Liste kommt zwar eine Erfolgsmeldung, die Doublette wird aber nicht gespeichert

        // ANMERKUNG:
        // durch das Umstellen auf die Objekte in den Listen, funktioniert das Schreiben der Datensätz nicht ganz wie gewollt
        // gewollt war den ersten Treffer in der Maske anzeigen zu lassen und alle weiteren in der Liste
        // jetzt werden immer alle in der liste angezeigt, aber auch der 1. Eintrag in der Maske

        // Verbindung mit DB aufbauen
        // Security token für Troubleshooting entfernt (Überlegung war, dass ich bei der Erstellung der lokalen Datenbank kein Passwort gesetzt hatte und es deshalb zum Fehler kam) 
        SqlConnection connection = new SqlConnection(@"yourlokalDBconnection");

        // Instanzvariable um Werte übergeben zu können
        private string old_AP_Nachname;
        private string old_KD_Name;

        // Funktion um Felder zu leeren
        private void ClearTextFields(params Control[] controls)
        {
            // für jedes Element was ich übergebe
            foreach (var control in controls)
            {
                // wenn es eine Textbox ist
                if (control is TextBox textBox)
                {
                    // setze sie auf leer
                    textBox.Text = string.Empty;
                }
                // wenn es eine ComboBox ist
                else if (control is ComboBox comboBox)
                {
                    // stelle sie auf nicht ausgewählt
                    comboBox.SelectedItem = null;
                }
            }
        }
        // Funktion um zu prüfen ob Eingaben Leer sind
        private bool AreFieldsEmpty(params string[] inputs)
        {
            return inputs.Any(string.IsNullOrWhiteSpace); // Gibt true zurück, wenn mindestens ein Feld leer ist (LINQ Ansatz)

        }
        // Funktion zur Überprüfung von doppelten Einträgen
        private bool CheckDoubleEntry(string tableName, Dictionary<string, object> parameters)
        {
            // öffnen der Verbindung zur DB
            connection.Open();

            // SQL-Abfrage zur Überprüfung auf doppelte Einträge
            string checkQuery = $"SELECT COUNT(*) FROM [{tableName}] WHERE ";

            // Dynamisch die WHERE-Klausel basierend auf den Parametern erstellen
            var conditions = new List<string>();
            foreach (var param in parameters)
            {
                conditions.Add($"{param.Key} = @{param.Key}");
            }
            checkQuery += string.Join(" AND ", conditions);

            using (SqlCommand checkCommand = new SqlCommand(checkQuery, connection))
            {
                // Parameter hinzufügen
                foreach (var param in parameters)
                {
                    checkCommand.Parameters.Add(new SqlParameter($"@{param.Key}", param.Value));
                }

                int count = (int)checkCommand.ExecuteScalar();

                // trennen der Verbindung zur DB
                connection.Close();
                return count > 0; // Gibt true zurück, wenn ein doppelter Eintrag gefunden wurde
            }
        }
        private bool CheckDoubleKD(string name, string straße, string ort, string plz, string land)
        {
            // Gibt vor wie die Tabelle aufgebaut ist
            var parameters = new Dictionary<string, object>
            {
                { "Name", name },
                { "Straße", straße },
                { "Ort", ort },
                { "PLZ", plz },
                { "Land", land }
            };

            return CheckDoubleEntry("Kunden", parameters);
        }
        private bool CheckDoubleAP(string anrede, string nachname, string vorname)
        {
            // Gibt vor wie die Tabelle aufgebaut ist
            var parameters = new Dictionary<string, object>
            {
                { "Anrede", anrede },
                { "Nachname", nachname },
                { "Vorname", vorname }
            };

            return CheckDoubleEntry("Personen", parameters);
        }
        // Funktion um den Ersten Buchstaben Groß zu schreiben
        private string CapitalizeFirstLetter(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input; // Rückgabe des Eingabewertes, wenn er leer ist

            // Den ersten Buchstaben groß schreiben und den Rest unverändert lassen
            return char.ToUpper(input[0]) + input.Substring(1);
        }
        // Funktion um Sonderzeichen aus Eingaben zu entfernen (& und . erlauben für Firmennamen + Leerzeichen)
        public string Interceptor(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty; // Rückgabe eines leeren Strings, wenn der Eingabestring null oder leer ist
            }

            // Verwendung von Regex, um alle nicht-alphanumerischen Zeichen zu entfernen, außer &, ß und . sowie Leerzeichen
            return Regex.Replace(input, @"[^a-zA-Z0-9&.ß\s]", string.Empty);
        }
        // Funktion zur Erstellung von SQL-Abfragen (INSERT, UPDATE, DELETE)
        // Methode mit Transaktionsparametern
        private void ExecuteSqlCommand(string query, Dictionary<string, object> parameters, SqlConnection connection, SqlTransaction transaction)
        {
            using (SqlCommand command = new SqlCommand(query, connection, transaction))
            {
                // Parameter hinzufügen
                foreach (var param in parameters)
                {
                    command.Parameters.Add(new SqlParameter($"@{param.Key}", param.Value));
                }

                try
                {
                    command.ExecuteNonQuery(); // Führt den SQL-Befehl aus
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Fehler beim Ausführen des SQL-Befehls: " + ex.Message);
                    throw; // Wirf die Ausnahme weiter, um die Transaktion zurückzurollen
                }
            }
        }
        // Überladene Methode ohne Transaktionsparameter
        private void ExecuteSqlCommand(string query, Dictionary<string, object> parameters)
        {
            using (SqlConnection connection = new SqlConnection(@"yourlokalDBconnection"))
            {
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    // Parameter hinzufügen
                    foreach (var param in parameters)
                    {
                        command.Parameters.Add(new SqlParameter($"@{param.Key}", param.Value));
                    }

                    try
                    {
                        connection.Open(); // Verbindung öffnen
                        command.ExecuteNonQuery(); // Führt den SQL-Befehl aus
                    }
                    catch (SqlException ex)
                    {
                        // Zeige spezifische Fehlermeldung für SQL-Fehler
                        MessageBox.Show("Fehler beim Ausführen des SQL-Befehls: " + ex.Message);
                        // Hier könnten auch Log-Mechanismen implementiert werden
                    }
                    catch (Exception ex)
                    {
                        // Allgemeine Fehlerbehandlung
                        MessageBox.Show("Ein unerwarteter Fehler ist aufgetreten: " + ex.Message);
                    }
                }
            }
        }

        // Funktion um Daten aus der Datenbank zu lesen (SELECT)
        // bei mehr als ein Datensatz
        private SqlDataReader ExecuteReaderCommand(string query, Dictionary<string, object> parameters)
        {
            SqlConnection connection = new SqlConnection(@"yourlokalDBconnection");
            SqlCommand command = new SqlCommand(query, connection);

            // Parameter hinzufügen
            foreach (var param in parameters)
            {
                command.Parameters.Add(new SqlParameter($"@{param.Key}", param.Value));
            }

            try
            {
                connection.Open();
                return command.ExecuteReader(CommandBehavior.CloseConnection); // Schließt die Verbindung, wenn der Reader geschlossen wird
            }
            catch (Exception ex)
            {
                MessageBox.Show("Fehler beim Ausführen des SQL-Befehls: " + ex.Message);
                connection.Close(); // Schließt die Verbindung im Fehlerfall
                return null; // Gibt null zurück, wenn ein Fehler auftritt
            }
        }
        // bei genau einem Wert
        private int ExecuteScalarCommand(string query, Dictionary<string, object> parameters)
        {
            using (SqlConnection connection = new SqlConnection(@"yourlokalDBconnection"))
            {
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    // Parameter hinzufügen
                    foreach (var param in parameters)
                    {
                        command.Parameters.Add(new SqlParameter($"@{param.Key}", param.Value));
                    }

                    try
                    {
                        connection.Open(); // Verbindung öffnen
                        return (int?)command.ExecuteScalar() ?? 0; // Gibt den ersten Wert der ersten Zeile zurück oder 0, wenn null
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Fehler beim Ausführen des SQL-Befehls: " + ex.Message);
                        return 0; // Gibt 0 zurück, wenn ein Fehler auftritt
                    }
                }
            }
        }
        // Funktion zur Button Aktivierung
        private void UpdateButtons()
        {
            // Den Button für "Zuordnung" aktivieren, wenn alle Felder gefüllt sind
            Zuordnung.IsEnabled = !AreFieldsEmpty(AP_Anrede.Text, AP_Name.Text, AP_Vorname.Text, KD_Name.Text, KD_Straße.Text, KD_Ort.Text, KD_PLZ.Text, KD_Straße.Text);

            // Den Button für "Ansprechpartner finden" aktivieren, wenn alle Kundenfelder gefüllt sind
            AP_Finden.IsEnabled = !AreFieldsEmpty(KD_Name.Text, KD_Straße.Text, KD_Ort.Text, KD_PLZ.Text, KD_Land.Text);

            // Den Button für "Kunden finden" aktivieren, wenn alle Personenfelder gefüllt sind
            KD_Finden.IsEnabled = !AreFieldsEmpty(AP_Anrede.Text, AP_Name.Text, AP_Vorname.Text);
        }
        // Funktion um Datensatz zu finden
        private void FindKunde()
        {
            // Parameter für die SQL-Abfrage
            var parameters = new Dictionary<string, object>();

            // Dynamische SQL-Abfrage erstellen
            StringBuilder queryBuilder = new StringBuilder("SELECT Name, Straße, Ort, PLZ, Land FROM Kunden WHERE 1=1");

            // Überprüfen und Hinzufügen der Bedingungen basierend auf den ausgefüllten Feldern
            AppendParameter(ref queryBuilder, parameters, "Name", KD_Name.Text);
            AppendParameter(ref queryBuilder, parameters, "Straße", KD_Straße.Text);
            AppendParameter(ref queryBuilder, parameters, "Ort", KD_Ort.Text);
            AppendParameter(ref queryBuilder, parameters, "PLZ", KD_PLZ.Text);
            AppendParameter(ref queryBuilder, parameters, "Land", KD_Land.Text);

            // SQL-Abfrage als String
            string query = queryBuilder.ToString();

            // Ausführen des SQL-Befehls
            using (var reader = ExecuteReaderCommand(query, parameters))
            {
                // Leeren der ObservableCollection vor dem Hinzufügen neuer Einträge
                Kunden.Clear();

                // Überprüfen, ob der Reader nicht null ist
                if (reader != null)
                {
                    // Überprüfen, ob Daten vorhanden sind
                    if (reader.HasRows)
                    {
                        // Liest den ersten Datensatz
                        if (reader.Read())
                        {
                            // Fülle die Textfelder mit dem ersten Datensatz
                            KD_Name.Text = reader["Name"].ToString();
                            KD_Straße.Text = reader["Straße"].ToString();
                            KD_Ort.Text = reader["Ort"].ToString();
                            KD_PLZ.Text = reader["PLZ"].ToString();
                            KD_Land.Text = reader["Land"].ToString();

                            // Speichern des alten Nachnamens für die spätere Aktualisierung
                            old_KD_Name = reader["Name"].ToString();

                            // Knopf ändern, wenn Datensatz gefunden
                            KD_Suchen.Content = "Ändern";
                        }

                        // Füge alle weiteren Einträge zur ObservableCollection hinzu
                        do
                        {
                            // Erstelle ein neues Kunde-Objekt für jeden weiteren Eintrag
                            var kunde = new Kunde
                            {
                                Name = reader["Name"].ToString(),
                                Straße = reader["Straße"].ToString(),
                                Ort = reader["Ort"].ToString(),
                                PLZ = reader["PLZ"].ToString(),
                                Land = reader["Land"].ToString()
                            };

                            // Füge das Kunde-Objekt zur ObservableCollection hinzu
                            Kunden.Add(kunde);

                        } while (reader.Read()); // Lese die nächsten Zeilen
                    }
                    else
                    {
                        MessageBox.Show("Kein Datensatz gefunden.");
                    }
                }
                else
                {
                    MessageBox.Show("Fehler beim Abrufen der Daten. Bitte versuchen Sie es erneut.");
                }
            }
        }
        private void FindPerson()
        {
            // Parameter für die SQL-Abfrage
            var parameters = new Dictionary<string, object>();

            // Dynamische SQL-Abfrage erstellen
            StringBuilder queryBuilder = new StringBuilder("SELECT Anrede, Nachname, Vorname FROM Personen WHERE 1=1");

            // Überprüfen und Hinzufügen der Bedingungen basierend auf den ausgefüllten Feldern
            AppendParameter(ref queryBuilder, parameters, "Anrede", AP_Anrede.Text);
            AppendParameter(ref queryBuilder, parameters, "Nachname", AP_Name.Text);
            AppendParameter(ref queryBuilder, parameters, "Vorname", AP_Vorname.Text);

            // SQL-Abfrage als String
            string query = queryBuilder.ToString();

            // Ausführen des SQL-Befehls
            using (var reader = ExecuteReaderCommand(query, parameters))
            {
                // Leeren der ObservableCollection vor dem Hinzufügen neuer Einträge
                Ansprechpartner.Clear();

                // Überprüfen, ob der Reader nicht null ist
                if (reader != null)
                {
                    // Überprüfen, ob Daten vorhanden sind
                    if (reader.HasRows)
                    {
                        // Liest den ersten Datensatz
                        if (reader.Read())
                        {
                            // Fülle die Textfelder mit dem ersten Datensatz
                            AP_Anrede.Text = reader["Anrede"].ToString();
                            AP_Name.Text = reader["Nachname"].ToString();
                            AP_Vorname.Text = reader["Vorname"].ToString();

                            // Speichern des alten Nachnamens für die spätere Aktualisierung
                            old_AP_Nachname = reader["Nachname"].ToString();

                            // Knopf ändern, wenn Datensatz gefunden
                            AP_Suchen.Content = "Ändern";
                        }

                        // Füge alle weiteren Einträge zur ObservableCollection hinzu
                        do
                        {
                            // Erstelle ein neues Person-Objekt für jeden weiteren Eintrag
                            var person = new Person
                            {
                                Anrede = reader["Anrede"].ToString(),
                                Nachname = reader["Nachname"].ToString(),
                                Vorname = reader["Vorname"].ToString(),
                            };

                            // Füge das Personen-Objekt zur ObservableCollection hinzu
                            Ansprechpartner.Add(person);

                        } while (reader.Read()); // Lese die nächsten Zeilen
                    }
                    else
                    {
                        MessageBox.Show("Kein Datensatz gefunden.");
                    }
                }
                else
                {
                    MessageBox.Show("Fehler beim Abrufen der Daten. Bitte versuchen Sie es erneut.");
                }
            }
        }
        // Funktion um auf leere Eingabefelder zu prüfen und die SQL Abfrage dynamisch zu bauen 
        private void AppendParameter(ref StringBuilder queryBuilder, Dictionary<string, object> parameters, string parameterName, string inputValue)
        {
            if (!string.IsNullOrWhiteSpace(inputValue))
            {
                parameters.Add(parameterName, Interceptor(inputValue.Trim()));
                queryBuilder.Append($" AND {parameterName} = @{parameterName}");
            }
        }
        // Funktion um Datensatz zu ändern
        private void UpdatePerson()
        {
            // Auslesen der Daten aus den Textboxen
            string anrede = Interceptor(AP_Anrede.Text.Trim());
            string vorname = Interceptor(AP_Vorname.Text.Trim());
            string newNachname = Interceptor(AP_Name.Text.Trim());

            // Prüfen auf leere Einträge
            if (AreFieldsEmpty(anrede, vorname, newNachname))
            {
                MessageBox.Show("Bitte füllen Sie alle erforderlichen Felder aus.");
                return;
            }

            // Überprüfen, ob ein ähnlicher Eintrag bereits existiert
            if (CheckDoubleAP(anrede, newNachname, vorname))
            {
                MessageBox.Show("Ein ähnlicher Eintrag existiert bereits. Bitte ändern Sie mindestens ein Feld.", "Eingabefehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return; // Abbrechen der Methode
            }

            // SQL-Abfrage zum Aktualisieren der Personendaten
            string updateQuery = "UPDATE Personen SET Anrede = @Anrede, Vorname = @Vorname, Nachname = @NewNachname WHERE Nachname = @OldNachname";

            // Parameter für die Update-Abfrage
            var parameters = new Dictionary<string, object>
            {
                { "OldNachname", old_AP_Nachname },
                { "Anrede", anrede },
                { "Vorname", CapitalizeFirstLetter(vorname) },
                { "NewNachname", CapitalizeFirstLetter(newNachname) }
            };

            // Ausführen der Aktualisierung
            ExecuteSqlCommand(updateQuery, parameters);

            // Erfolgsmeldung
            MessageBox.Show("Datensatz erfolgreich aktualisiert.");

            // Button-Text zurücksetzen
            AP_Suchen.Content = "Suchen";

            // Textfelder leeren
            ClearTextFields(AP_Anrede, AP_Vorname, AP_Name);
        }
        private void UpdateKunden()
        {
            // Auslesen der Daten aus den Textboxen
            string newName = Interceptor(KD_Name.Text.Trim());
            string straße = Interceptor(KD_Straße.Text.Trim());
            string ort = Interceptor(KD_Ort.Text.Trim());
            string plz = Interceptor(KD_PLZ.Text.Trim());
            string land = Interceptor(KD_Land.Text.Trim());

            // Überprüfen, ob die Felder ausgefüllt sind
            if (AreFieldsEmpty(newName, straße, ort, plz, land))
            {
                MessageBox.Show("Bitte füllen Sie alle erforderlichen Felder aus.");
                return;
            }

            // Überprüfen, ob ein ähnlicher Eintrag bereits existiert
            if (CheckDoubleKD(newName, straße, ort, plz, land))
            {
                MessageBox.Show("Ein ähnlicher Eintrag existiert bereits. Bitte ändern Sie mindestens ein Feld.", "Eingabefehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return; // Abbrechen der Methode
            }

            // SQL-Abfrage zum Aktualisieren der Kundendaten
            string updateQuery = "UPDATE Kunden SET Name = @newName, Straße = @Straße, Ort = @Ort, PLZ = @PLZ, Land = @Land WHERE Name = @oldName";

            // Parameter für die Update-Abfrage
            var parameters = new Dictionary<string, object>
            {
                { "oldName", old_KD_Name },
                { "newName", CapitalizeFirstLetter(newName) },
                { "Straße", CapitalizeFirstLetter(straße) },
                { "Ort", CapitalizeFirstLetter(ort) },
                { "PLZ", plz },
                { "Land", CapitalizeFirstLetter(land) }
            };

            // Ausführen der Aktualisierung
            ExecuteSqlCommand(updateQuery, parameters);

            // Erfolgsmeldung
            MessageBox.Show("Datensatz erfolgreich aktualisiert.");

            // Button-Text zurücksetzen
            KD_Suchen.Content = "Suchen";

            // Textfelder leeren
            ClearTextFields(KD_Name, KD_Straße, KD_Ort, KD_PLZ, KD_Land);
        }
        // Funktionen um Datensätze zuzuordnen
        private void KDforAP(int kundenID, int personenID)
        {
            // SQL-Abfrage um zu prüfen, ob bereits eine Verknüpfung besteht
            string checkQuery = "SELECT COUNT(*) FROM KundenAnsprechpartner WHERE KundenID = @KundenID AND PersonenID = @PersonenID";
            var checkParameters = new Dictionary<string, object>
            {
                { "KundenID", kundenID },
                { "PersonenID", personenID }
            };

            // Prüfen, ob eine Verknüpfung existiert
            int count = ExecuteScalarCommand(checkQuery, checkParameters);
            if (count > 0)
            {
                MessageBox.Show("Es besteht bereits eine Verknüpfung zwischen diesem Kunden und diesem Ansprechpartner.");
                return; // Funktion beenden, wenn bereits eine Verknüpfung besteht
            }

            // SQL-Abfrage um die KundenID in der Personen-Tabelle zu aktualisieren
            string updateQuery = "UPDATE Personen SET KundenID = @KundenID WHERE ID = @PersonenID";
            var updateParameters = new Dictionary<string, object>
            {
                { "KundenID", kundenID },
                { "PersonenID", personenID }
            };

            // Ausführen der Aktualisierung
            ExecuteSqlCommand(updateQuery, updateParameters);

            // SQL-Abfrage um KundenID der AnsprechpartnerID zuzuordnen
            string insertQuery = "INSERT INTO KundenAnsprechpartner (KundenID, PersonenID) VALUES (@KundenID, @PersonenID)";
            var insertParameters = new Dictionary<string, object>
            {
                { "KundenID", kundenID },
                { "PersonenID", personenID }
            };

            // Ausführen der Aktualisierung
            ExecuteSqlCommand(insertQuery, insertParameters);

            // Erfolgsmeldung
            string message = $"Folgende Datensätze wurden verknüpft:\n\n" +
                             $"Kunde: {kundenID}\n" +
                             $"Ansprechpartner: {personenID}\n";
            MessageBox.Show(message);

            // Textfelder leeren
            ClearTextFields(KD_Name, KD_Straße, KD_Ort, KD_PLZ, KD_Land, AP_Anrede, AP_Name, AP_Vorname);

            // Setze die Knöpfe zurück
            AP_Suchen.Content = "Suchen";
            KD_Suchen.Content = "Suchen";
        }
        private int GetKundenID()
        {
            // Auslesen der Daten aus der Maske
            string KD = Interceptor(KD_Name.Text.Trim());

            // SQL-Abfrage für Kunden ID
            string query = "SELECT ID FROM Kunden WHERE Name = @Name";
            var parameters = new Dictionary<string, object>
            {
                { "Name", KD }
            };

            // Verwenden der ExecuteScalarCommand-Methode, um die Kunden-ID abzurufen
            return ExecuteScalarCommand(query, parameters);
        }
        private int GetPersonenID()
        {
            // Auslesen der Daten aus der Maske
            string AP = Interceptor(AP_Name.Text.Trim());

            // SQL-Abfrage für Kunden ID
            string query = "SELECT ID FROM Personen WHERE Nachname = @Nachname";
            var parameters = new Dictionary<string, object>
            {
                { "Nachname", AP }
            };

            // Verwenden der ExecuteScalarCommand-Methode, um die Kunden-ID abzurufen
            return ExecuteScalarCommand(query, parameters);
        }
        // Funktion um Datensatz inkl. Verknüpfung zu löschen
        private void DeleteKD()
        {
            // SQL-Abfragen für das Löschen
            string queryDeleteAP_KD = "DELETE FROM [dbo].[KundenAnsprechpartner] WHERE KundenID IN (SELECT ID FROM [dbo].[Kunden] WHERE Name = @Name AND Straße = @Straße AND Ort = @Ort AND PLZ = @PLZ AND Land = @Land)";
            string queryDeleteKunden = "DELETE FROM [dbo].[Kunden] WHERE Name = @Name AND Straße = @Straße AND Ort = @Ort AND PLZ = @PLZ AND Land = @Land";

            // Parameter für die Löschabfragen
            var parameters = new Dictionary<string, object>
            {
                { "Name", Interceptor(KD_Name.Text.Trim()) },
                { "Straße", Interceptor(KD_Straße.Text.Trim()) },
                { "Ort", Interceptor(KD_Ort.Text.Trim()) },
                { "PLZ", Interceptor(KD_PLZ.Text.Trim()) },
                { "Land", Interceptor(KD_Land.Text.Trim()) }
            };
            connection.Open(); // Manuelles Öffnen der Verbinung DB zum Troubleshooting
            using (SqlTransaction transaction = connection.BeginTransaction())
            {
                try
                {
                    // Löschen der KundenAnsprechpartner
                    ExecuteSqlCommand(queryDeleteAP_KD, parameters, connection, transaction);

                    // Löschen der Kunden
                    ExecuteSqlCommand(queryDeleteKunden, parameters, connection, transaction);

                    // Bestätigen der Transaktion
                    transaction.Commit();

                    // Erfolgsmeldung
                    MessageBox.Show("Datensatz erfolgreich gelöscht.");
                }
                catch (Exception ex)
                {
                    // Bei einem Fehler die Transaktion zurückrollen
                    transaction.Rollback();
                    connection.Close(); // Manuelles trennen der Verbinung DB zum Troubleshooting
                    MessageBox.Show("Fehler beim Löschen des Datensatzes: " + ex.Message);
                }
            }
            connection.Close(); // Manuelles trennen der Verbinung DB zum Troubleshooting
        }
        private void DeletePerson()
        {
            // SQL-Abfragen für das Löschen
            string queryDeleteKD_AP = "DELETE FROM [dbo].[KundenAnsprechpartner] WHERE PersonenID IN (SELECT ID FROM [dbo].[Personen] WHERE Anrede = @Anrede AND Vorname = @Vorname AND Nachname = @Nachname)";
            string queryDeletePersonen = "DELETE FROM [dbo].[Personen] WHERE Anrede = @Anrede AND Vorname = @Vorname AND Nachname = @Nachname";

            // Parameter für die Löschabfragen
            var parameters = new Dictionary<string, object>
            {
                { "Anrede", Interceptor(AP_Anrede.Text.Trim()) },
                { "Vorname", Interceptor(AP_Vorname.Text.Trim()) },
                { "Nachname", Interceptor(AP_Name.Text.Trim()) }
            };
            connection.Open(); // Manuelles Öffnen der Verbinung DB zum Troubleshooting
            using (SqlTransaction transaction = connection.BeginTransaction())
            {
                try
                {
                    // Löschen der KundenAnsprechpartner
                    ExecuteSqlCommand(queryDeleteKD_AP, parameters, connection, transaction);

                    // Löschen der Personen
                    ExecuteSqlCommand(queryDeletePersonen, parameters, connection, transaction);

                    // Bestätigen der Transaktion
                    transaction.Commit();

                    // Erfolgsmeldung
                    MessageBox.Show("Datensatz erfolgreich gelöscht.");
                }
                catch (Exception ex)
                {
                    // Bei einem Fehler die Transaktion zurücksetzen
                    transaction.Rollback();
                    connection.Close(); // Manuelles trennen der Verbinung DB zum Troubleshooting
                    MessageBox.Show("Fehler beim Löschen der Datensätze: " + ex.Message);
                }
            }
            connection.Close(); // Manuelles trennen der Verbinung DB zum Troubleshooting
        }
        // Funktion zur Anzeige der Ansprechpartner des Kunden
        private void FindAPforKD(int kundenID)
        {
            // Überprüfen, ob die KundenID gültig ist
            if (kundenID <= 0)
            {
                MessageBox.Show("Ungültige Kunden-ID.", "Eingabefehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return; // Methode abbrechen, wenn die Kunden-ID ungültig ist
            }

            // SQL-Befehl zum Abrufen der Daten für die gegebene KundenID
            string query = "SELECT Vorname, Nachname, Anrede FROM [dbo].[Personen] WHERE KundenID = @KundenID";
            var parameters = new Dictionary<string, object>
            {
                { "KundenID", kundenID }
            };

            using (var reader = ExecuteReaderCommand(query, parameters))
            {
                // Überprüfen, ob der Reader nicht null ist
                if (reader != null)
                {
                    // Leeren der ObservableCollection vor dem Hinzufügen neuer Einträge
                    Ansprechpartner.Clear();

                    // Überprüfen, ob der Reader nicht null ist
                    if (reader != null)
                    {
                        // Überprüfen, ob Daten vorhanden sind
                        if (reader.HasRows)
                        {
                            // Liest den ersten Datensatz
                            if (reader.Read())
                            {
                                // Fülle die Textfelder mit dem ersten Datensatz
                                AP_Anrede.Text = reader["Anrede"].ToString();
                                AP_Name.Text = reader["Nachname"].ToString();
                                AP_Vorname.Text = reader["Vorname"].ToString();

                            }

                            // Füge alle weiteren Einträge zur ObservableCollection hinzu
                            do
                            {
                                // Erstelle ein neues Person-Objekt für jeden weiteren Eintrag
                                var person = new Person
                                {
                                    Anrede = reader["Anrede"].ToString(),
                                    Nachname = reader["Nachname"].ToString(),
                                    Vorname = reader["Vorname"].ToString(),
                                };

                                // Füge das Personen-Objekt zur ObservableCollection hinzu
                                Ansprechpartner.Add(person);

                            } while (reader.Read()); // Lese die nächsten Zeilen
                        }
                        else
                        {
                            MessageBox.Show("Kein Datensatz gefunden.");
                        }
                    }
                    else
                    {
                        MessageBox.Show("Fehler beim Abrufen der Daten. Bitte versuchen Sie es erneut.");
                    }
                }
            }
        }
        // Funktion zur Anzeige der Kunden des Ansprechpartners
        private void FindKDfromAP(int personenID)
        {
            // Überprüfen, ob die PersonenID gültig ist
            if (personenID <= 0)
            {
                MessageBox.Show("Ungültige Personen-ID.", "Eingabefehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return; // Methode abbrechen, wenn die Kunden-ID ungültig ist
            }

            // SQL-Befehl zum Abrufen der Daten für die gegebene PersonenID
            string query = "SELECT k.Name, k.Straße, k.Ort, k.PLZ, k.Land" +
                            " FROM [dbo].[Kunden] k" +
                            " JOIN [dbo].[KundenAnsprechpartner] ka" +
                            " ON k.ID = ka.KundenID" +
                            " WHERE ka.PersonenID = @PersonenID";
            var parameters = new Dictionary<string, object>
            {
                { "PersonenID", personenID }
            };

            using (var reader = ExecuteReaderCommand(query, parameters))
            {
                // Überprüfen, ob der Reader nicht null ist
                if (reader != null)
                {
                    // Leeren der ObservableCollection vor dem Hinzufügen neuer Einträge
                    Kunden.Clear();

                    // Überprüfen, ob der Reader nicht null ist
                    if (reader != null)
                    {
                        // Überprüfen, ob Daten vorhanden sind
                        if (reader.HasRows)
                        {
                            // Liest den ersten Datensatz
                            if (reader.Read())
                            {
                                // Fülle die Textfelder mit dem ersten Datensatz
                                KD_Name.Text = reader["Name"].ToString();
                                KD_Straße.Text = reader["Straße"].ToString();
                                KD_Ort.Text = reader["Ort"].ToString();
                                KD_PLZ.Text = reader["PLZ"].ToString();
                                KD_Land.Text = reader["Land"].ToString();

                            }

                            // Füge alle weiteren Einträge zur ObservableCollection hinzu
                            do
                            {
                                // Erstelle ein neues Kunde-Objekt für jeden weiteren Eintrag
                                var kunde = new Kunde
                                {
                                    Name = reader["Name"].ToString(),
                                    Straße = reader["Straße"].ToString(),
                                    Ort = reader["Ort"].ToString(),
                                    PLZ = reader["PLZ"].ToString(),
                                    Land = reader["Land"].ToString()
                                };

                                // Füge das Kunde-Objekt zur ObservableCollection hinzu
                                Kunden.Add(kunde);

                            } while (reader.Read()); // Lese die nächsten Zeilen
                        }
                        else
                        {
                            MessageBox.Show("Kein Datensatz gefunden.");
                        }
                    }
                    else
                    {
                        MessageBox.Show("Fehler beim Abrufen der Daten. Bitte versuchen Sie es erneut.");
                    }
                }
            }
        }
        // Klassen zur Bearbeitung in der Liste
        public class Kunde
        {
            public string Name { get; set; }
            public string Straße { get; set; }
            public string Ort { get; set; }
            public string PLZ { get; set; }
            public string Land { get; set; }
        }
        public class Person
        {
            public string Anrede { get; set; }
            public string Vorname { get; set; }
            public string Nachname { get; set; }
        }
        // Observer der Listen
        public ObservableCollection<Kunde> Kunden { get; set; }
        public ObservableCollection<Person> Ansprechpartner { get; set; }
        //
        // Eventhandler:
        //
        // Neuer Kunde
        private void NewKunde_Click(object sender, RoutedEventArgs e)
        {
            // Prüfe ob Felder leer und gehe nur weiter wenn alle Felder voll sind
            if (AreFieldsEmpty(KD_Name.Text, KD_Straße.Text, KD_Ort.Text, KD_PLZ.Text, KD_Land.Text))
            {
                MessageBox.Show("Bitte füllen Sie alle Felder aus.", "Eingabefehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return; // Abbrechen der Methode, wenn ein Feld leer ist
            }

            // Überprüfen, ob ein gleicher Eintrag bereits existiert
            if (CheckDoubleKD(KD_Name.Text, KD_Straße.Text, KD_Ort.Text, KD_PLZ.Text, KD_Land.Text))
            {
                MessageBox.Show("Ein ähnlicher Eintrag existiert bereits. Bitte ändern Sie mindestens ein Feld.", "Eingabefehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return; // Abbrechen der Methode
            }

            // SQL-Befehl zum Einfügen der Daten
            string query = "INSERT INTO [dbo].[Kunden] (Name, Straße, Ort, PLZ, Land) VALUES (@Name, @Straße, @Ort, @PLZ, @Land)";

            // Parameter für die Einfügeabfrage
            var parameters = new Dictionary<string, object>
            {
                { "Name", CapitalizeFirstLetter(Interceptor(KD_Name.Text).Trim()) },
                { "Straße", CapitalizeFirstLetter(Interceptor(KD_Straße.Text).Trim()) },
                { "Ort", CapitalizeFirstLetter(Interceptor(KD_Ort.Text).Trim()) },
                { "PLZ", Interceptor(KD_PLZ.Text.Trim()) },
                { "Land", CapitalizeFirstLetter(Interceptor(KD_Land.Text).Trim()) }
            };

            // Ausführen des Einfügebefehls
            ExecuteSqlCommand(query, parameters);

            // Erfolgsmeldung
            MessageBox.Show("Daten erfolgreich gespeichert.");

            // Knopf zur Datensatzsuche aktualisieren
            KD_Suchen.Content = "Suchen";

            // Textfelder leeren
            ClearTextFields(KD_Name, KD_Straße, KD_Ort, KD_PLZ, KD_Land);
        }
        // Kunde suchen -> Kunde ändern
        private void SearchKunde_Click(object sender, RoutedEventArgs e)
        {
            // Wenn der Knopf auf "suchen" steht
            if (KD_Suchen.Content.ToString() == "Suchen")
            {
                // Führe Funktion aus
                FindKunde();
            }
            // Wenn der Knopf auf "ändern" steht
            else if (KD_Suchen.Content.ToString() == "Ändern")
            {
                // Führe Funktion aus
                UpdateKunden();
            }
        }
        // Kunde löschen
        private void DeleteKunde_Click(object sender, RoutedEventArgs e)
        {
            // SQL-Befehl zum Laden der Daten
            string query = "SELECT COUNT(*) FROM [dbo].[Kunden] WHERE Name = @Name AND Straße = @Straße AND Ort = @Ort AND PLZ = @PLZ AND Land = @Land";
            var parameters = new Dictionary<string, object>
            {
                { "Name", Interceptor(KD_Name.Text.Trim()) },
                { "Straße", Interceptor(KD_Straße.Text.Trim()) },
                { "Ort", Interceptor(KD_Ort.Text.Trim()) },
                { "PLZ", Interceptor(KD_PLZ.Text.Trim()) },
                { "Land", Interceptor(KD_Land.Text.Trim()) }
            };

            // Überprüfen, ob der Datensatz existiert
            int count = ExecuteScalarCommand(query, parameters);

            if (count > 0)
            {
                // Erfolgsmeldung mit den Details des Datensatzes
                string message = $"Der Datensatz mit den folgenden Details wurde gefunden:\n\n" +
                                 $"Name: {KD_Name.Text}\n" +
                                 $"Straße: {KD_Straße.Text}\n" +
                                 $"Ort: {KD_Ort.Text}\n" +
                                 $"PLZ: {KD_PLZ.Text}\n" +
                                 $"Land: {KD_Land.Text}\n\n" +
                                 "Möchten Sie diesen Datensatz wirklich löschen?";

                // Bestätigungsdialog anzeigen
                MessageBoxResult result = MessageBox.Show(message, "Datensatz löschen", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Löschen der Daten
                    DeleteKD();

                    // Textfelder leeren
                    ClearTextFields(KD_Name, KD_Straße, KD_Ort, KD_PLZ, KD_Land);

                    // Setze Knopf zurück
                    KD_Suchen.Content = "Suchen";
                }
                else
                {
                    MessageBox.Show("Abgebrochen!");
                }
            }
            else
            {
                MessageBox.Show("Kein Datensatz gefunden, der gelöscht werden kann.");
            }
        }
        // Neuer AP
        private void NewPerson_Click(object sender, RoutedEventArgs e)
        {
            // Prüfe ob Felder leer und gehe nur weiter wenn alle Felder voll sind
            if (AreFieldsEmpty(AP_Anrede.Text, AP_Name.Text, AP_Vorname.Text))
            {
                MessageBox.Show("Bitte füllen Sie alle Felder aus.", "Eingabefehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return; // Abbrechen der Methode, wenn ein Feld leer ist
            }

            // Überprüfen, ob ein gleicher Eintrag bereits existiert
            if (CheckDoubleAP(AP_Anrede.Text, AP_Name.Text, AP_Vorname.Text))
            {
                MessageBox.Show("Ein ähnlicher Eintrag existiert bereits. Bitte ändern Sie mindestens ein Feld.", "Eingabefehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                return; // Abbrechen der Methode
            }

            // SQL-Befehl zum Einfügen der Daten
            string query = "INSERT INTO [dbo].[Personen] (KundenID, Anrede, Vorname, Nachname) VALUES (@KundenID, @Anrede, @Vorname, @Nachname)";

            // Parameter für die Einfügeabfrage
            var parameters = new Dictionary<string, object>
            {
                { "KundenID", 1 }, // Hier wird die KundenID manuell auf 1 gesetzt, dies sollte entsprechend angepasst werden
                { "Anrede", Interceptor(AP_Anrede.Text.Trim()) },
                { "Vorname", CapitalizeFirstLetter(Interceptor(AP_Vorname.Text.Trim())) },
                { "Nachname", CapitalizeFirstLetter(Interceptor(AP_Name.Text.Trim())) }
            };

            // Ausführen des Einfügebefehls
            ExecuteSqlCommand(query, parameters);

            // Erfolgsmeldung
            MessageBox.Show("Daten erfolgreich gespeichert." + Environment.NewLine +
                            "Ansprechpartner Kunden 1 zugewiesen." + Environment.NewLine +
                            "Für weitere Zuweisungen bitte 'Kunde zuordnen' wählen.");

            // Knopf zur Datensatzsuche aktualisieren
            AP_Suchen.Content = "Suchen";

            // Textfelder leeren
            ClearTextFields(AP_Anrede, AP_Vorname, AP_Name);
        }
        // AP suchen -> AP Update
        private void SearchPerson_Click(object sender, RoutedEventArgs e)
        {
            // Wenn der Knopf auf "suchen" steht
            if (AP_Suchen.Content.ToString() == "Suchen")
            {
                // Führe Funktion aus
                FindPerson();
            }
            // Wenn der Knopf auf "ändern" steht
            else if (AP_Suchen.Content.ToString() == "Ändern")
            {
                // Führe Funktion aus
                UpdatePerson();
            }
        }
        // AP löschen
        private void DeletePerson_Click(object sender, RoutedEventArgs e)
        {
            // SQL-Befehl zum Laden der Daten
            string query = "SELECT COUNT(*) FROM [dbo].[Personen] WHERE Anrede = @Anrede AND Vorname = @Vorname AND Nachname = @Nachname";
            var parameters = new Dictionary<string, object>
            {
                { "Anrede", Interceptor(AP_Anrede.Text.Trim()) },
                { "Vorname", Interceptor(AP_Vorname.Text.Trim()) },
                { "Nachname", Interceptor(AP_Name.Text.Trim()) }
            };

            // Überprüfen, ob der Datensatz existiert
            int count = ExecuteScalarCommand(query, parameters);

            if (count > 0)
            {
                // Erfolgsmeldung mit den Details des Datensatzes
                string message = $"Der Datensatz mit den folgenden Details wurde gefunden:\n\n" +
                                 $"Anrede: {AP_Anrede.Text}\n" +
                                 $"Vorname: {AP_Vorname.Text}\n" +
                                 $"Nachname: {AP_Name.Text}\n\n" +
                                 "Möchten Sie diesen Datensatz wirklich löschen?";

                // Bestätigungsdialog anzeigen
                MessageBoxResult result = MessageBox.Show(message, "Datensatz löschen", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Löschen des Datensatzes
                    DeletePerson();

                    // Knopf zurücksetzen
                    AP_Suchen.Content = "Suchen";

                    // Textfelder leeren
                    ClearTextFields(AP_Anrede, AP_Vorname, AP_Name);
                }
                else
                {
                    MessageBox.Show("Abgebrochen!");
                }
            }
            else
            {
                MessageBox.Show("Kein Datensatz gefunden, der gelöscht werden kann.");
            }
        }
        // Kunden Ansprechpartner zuordnen
        private void KDforAP_Click(object sender, RoutedEventArgs e)
        {
            // Finden der aktuellen IDs und anschließende Trennung, da die Get-Funktionen die Verbindung geöffnet lassen
            int KundenID = GetKundenID();
            connection.Close();
            int PersonenID = GetPersonenID();
            connection.Close();

            // Wenn IDs gefunden
            if (KundenID > 0 && PersonenID > 0)
            {
                // Ausführen der Funktion zum Zuordnen Kunde -> Ansprechpartner
                KDforAP(KundenID, PersonenID);
            }
            else
            {
                MessageBox.Show("Es wurde kein gültiger Kunde oder Ansprechpartner gefunden.");
            }
        }
        // "Kunde zuordnen" und "Ansprechpartner finden" soll nur Möglich sein, wenn alle Felder gefüllt sind, bzw. alle Kundenfelder
        // Dazu Textboxen und ComboBox überwachen
        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Wenn die Felder sich ändern, prüfe ob ausgefüllt
            UpdateButtons();
        }
        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Wenn die ComboBox sich ändert, prüfe ob ausgefüllt
            UpdateButtons();
        }
        // Ansprechpartner zum aktuellen Kunden finden
        private void AP_Finden_Click(object sender, RoutedEventArgs e)
        {
            // Finden der aktuellen ID und anschließende Trennung, da die Get-Funktion die Verbindung geöffnet lässt
            int KundenID = GetKundenID();
            connection.Close();

            // Wenn IDs gefunden
            if (KundenID > 0)
            {
                // Ausführen der Funktion zum Anzeigen des Ansprechpartners
                FindAPforKD(KundenID);
            }
            else
            {
                MessageBox.Show("Es wurde kein gültiger Kunde oder Ansprechpartner gefunden.");
            }

        }
        // Kunden zum aktuellen Ansprechpartner finden
        private void KD_Finden_Click(object sender, RoutedEventArgs e)
        {
            // Finden der aktuellen ID und anschließende Trennung, da die Get-Funktion die Verbindung geöffnet lässt
            int PersonenID = GetPersonenID();
            connection.Close();

            // Wenn IDs gefunden
            if (PersonenID > 0)
            {
                // Ausführen der Funktion zum Anzeigen des Ansprechpartners
                FindKDfromAP(PersonenID);
            }
            else
            {
                MessageBox.Show("Es wurde kein gültiger Kunde oder Ansprechpartner gefunden.");
            }

        }
        // Alle Felder löschen
        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            // Löschen der Einträge
            ClearTextFields(KD_Name, KD_Straße, KD_Ort, KD_PLZ, KD_Land, AP_Anrede, AP_Name, AP_Vorname);

            // Zurücksetzen der Knöpfe
            AP_Suchen.Content = "Suchen";
            KD_Suchen.Content = "Suchen";

            // Löschen der Listen
            Kunden.Clear();
            Ansprechpartner.Clear();
        }
        // Erstellen von Listen
        private void ListKD_Click(object sender, RoutedEventArgs e)
        {
            Kunden.Clear(); // Leere die ObservableCollection

            string query = "SELECT Name, Straße, Ort, PLZ, Land FROM Kunden";

            using (var reader = ExecuteReaderCommand(query, new Dictionary<string, object>()))
            {
                if (reader != null)
                {
                    while (reader.Read())
                    {
                        Kunden.Add(new Kunde
                        {
                            Name = reader["Name"].ToString(),
                            Straße = reader["Straße"].ToString(),
                            Ort = reader["Ort"].ToString(),
                            PLZ = reader["PLZ"].ToString(),
                            Land = reader["Land"].ToString()
                        });
                    }
                }
                else
                {
                    MessageBox.Show("Keine Daten gefunden.");
                }
            }
        }
        private void ListAP_Click(object sender, RoutedEventArgs e)
        {
            Ansprechpartner.Clear(); // Leere die ObservableCollection

            string query = "SELECT Anrede, Nachname, Vorname FROM Personen";

            using (var reader = ExecuteReaderCommand(query, new Dictionary<string, object>()))
            {
                if (reader != null)
                {
                    while (reader.Read())
                    {
                        Ansprechpartner.Add(new Person
                        {
                            Anrede = reader["Anrede"].ToString(),
                            Vorname = reader["Vorname"].ToString(),
                            Nachname = reader["Nachname"].ToString()
                        });
                    }
                }
                else
                {
                    MessageBox.Show("Keine Daten gefunden.");
                }
            }
        }
        // Speichern der Daten bei Doppelclick
        private void ListeKunde_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ListeKunde.SelectedItem is Kunde selectedKunde)
            {
                // Überprüfen, ob die Eingabefelder leer sind
                if (string.IsNullOrWhiteSpace(Interceptor(selectedKunde.Name))||
                    string.IsNullOrWhiteSpace(Interceptor(selectedKunde.Straße)) ||
                    string.IsNullOrWhiteSpace(Interceptor(selectedKunde.Ort)) ||
                    string.IsNullOrWhiteSpace(Interceptor(selectedKunde.PLZ)) ||
                    string.IsNullOrWhiteSpace(Interceptor(selectedKunde.Land)))
                {
                    MessageBox.Show("Bitte füllen Sie alle Felder aus.", "Eingabefehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return; // Abbrechen, wenn ein Feld leer ist
                }

                // Überprüfen auf doppelte Einträge
                if (CheckDoubleKD(Interceptor(selectedKunde.Name), Interceptor(selectedKunde.Straße), Interceptor(selectedKunde.Ort), Interceptor(selectedKunde.PLZ), Interceptor(selectedKunde.Land)))
                {
                    MessageBox.Show("Ein ähnlicher Eintrag existiert bereits. Bitte ändern Sie mindestens ein Feld.", "Eingabefehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return; // Abbrechen, wenn ein doppelter Eintrag gefunden wurde
                }

                // Speichern der Änderungen in der Datenbank
                string updateQuery = "UPDATE Kunden SET Name = @Name, Straße = @Straße, Ort = @Ort, PLZ = @PLZ, Land = @Land WHERE Name = @OldName";

                var parameters = new Dictionary<string, object>
                {
                    { "OldName", (selectedKunde.Name) }, // Originalname für die WHERE-Klausel
                    { "Name", CapitalizeFirstLetter(Interceptor(selectedKunde.Name)) },
                    { "Straße", CapitalizeFirstLetter(Interceptor(selectedKunde.Straße)) },
                    { "Ort", CapitalizeFirstLetter(Interceptor(selectedKunde.Ort)) },
                    { "PLZ", Interceptor(selectedKunde.PLZ) },
                    { "Land", CapitalizeFirstLetter(Interceptor(selectedKunde.Land)) }
                };

                ExecuteSqlCommand(updateQuery, parameters);
                MessageBox.Show("Änderungen gespeichert.");
            }
            else
            {
                MessageBox.Show("Die eingegebenen Daten sind ungültig. Bitte überprüfen Sie Ihre Eingaben.", "Eingabefehler", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        private void ListeAP_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ListeAP.SelectedItem is Person selectedAP)
            {
                // Überprüfen auf leere Felder
                if (string.IsNullOrWhiteSpace(Interceptor(selectedAP.Anrede))   ||
                    string.IsNullOrWhiteSpace(Interceptor(selectedAP.Vorname)) ||
                    string.IsNullOrWhiteSpace(Interceptor(selectedAP.Nachname)))
                {
                    MessageBox.Show("Bitte füllen Sie alle Felder aus.", "Eingabefehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return; // Abbrechen, wenn ein Feld leer ist
                }

                // Überprüfen auf doppelte Einträge (falls benötigt)
                if (CheckDoubleAP(selectedAP.Anrede, selectedAP.Vorname, selectedAP.Nachname))
                {
                    MessageBox.Show("Ein ähnlicher Ansprechpartner existiert bereits. Bitte ändern Sie mindestens ein Feld.", "Eingabefehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return; // Abbrechen, wenn ein doppelter Eintrag gefunden wurde
                }

                // Speichern der Änderungen in der Datenbank
                string updateQuery = "UPDATE Personen SET Anrede = @Anrede, Vorname = @Vorname, Nachname = @Nachname WHERE Anrede = @OldAnrede AND Nachname = @OldNachname";

                var parameters = new Dictionary<string, object>
                {
                    { "OldAnrede", selectedAP.Anrede }, // Originalanrede für die WHERE-Klausel
                    { "OldNachname", selectedAP.Nachname },
                    { "Anrede", CapitalizeFirstLetter(Interceptor(selectedAP.Anrede))   }, // Kapitalisierung der Anrede
                    { "Vorname", CapitalizeFirstLetter(Interceptor(selectedAP.Vorname)) }, // Kapitalisierung des Vornamens
                    { "Nachname", CapitalizeFirstLetter(Interceptor(selectedAP.Nachname)) } // Kapitalisierung des Nachnamens
                };

                ExecuteSqlCommand(updateQuery, parameters);
                MessageBox.Show("Änderungen gespeichert.");
            }        
        }
    }
}